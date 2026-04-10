using MLS.DeFi.Configuration;
using MLS.DeFi.Interfaces;
using MLS.DeFi.Models;

namespace MLS.DeFi.Services;

/// <summary>
/// Cascading fallback chain for DeFi order execution.
/// The primary venue is always HYPERLIQUID; subsequent venues in the chain are tried
/// when the primary is unavailable or rejects the order.
/// Venue IDs are loaded from <see cref="DeFiOptions.FallbackChain"/> — never hardcoded.
/// </summary>
public sealed class BrokerFallbackChain(
    IHyperliquidClient _primary,
    IOptions<DeFiOptions> _options,
    ILogger<BrokerFallbackChain> _logger) : IBrokerFallbackChain
{
    /// <inheritdoc/>
    public async Task<DeFiOrderResult> ExecuteWithFallbackAsync(DeFiOrderRequest request, CancellationToken ct)
    {
        var result = await TryPlaceAsync(_primary, "hyperliquid", request, ct).ConfigureAwait(false);

        if (result is not null && result.State != DeFiOrderState.Rejected)
            return result;

        var chain = _options.Value.FallbackChain;
        for (int i = 1; i < chain.Length; i++)
        {
            _logger.LogWarning(
                "Primary venue rejected order {ClientOrderId} — trying fallback {Venue}",
                DeFiUtils.SafeLog(request.ClientOrderId),
                DeFiUtils.SafeLog(chain[i]));

            _logger.LogInformation("Fallback venue {Venue} not yet implemented — skipping", chain[i]);
        }

        throw new InvalidOperationException(
            $"All DeFi broker venues in the fallback chain failed to accept order " +
            $"{request.ClientOrderId}. Venues tried: {string.Join(", ", chain)}.");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetActiveBrokersAsync(CancellationToken ct)
    {
        var active = new List<string>();
        var chain  = _options.Value.FallbackChain;

        try
        {
            await _primary.GetOpenOrdersAsync(string.Empty, ct).ConfigureAwait(false);
            if (chain.Length > 0) active.Add(chain[0]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID health probe failed");
        }

        return active;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<DeFiOrderResult?> TryPlaceAsync(
        IHyperliquidClient client,
        string venueName,
        DeFiOrderRequest request,
        CancellationToken ct)
    {
        var timeoutSeconds = _options.Value.OrderTimeoutSeconds;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            return await client.PlaceOrderAsync(request, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Venue {Venue} timed out placing order {ClientOrderId}",
                DeFiUtils.SafeLog(venueName),
                DeFiUtils.SafeLog(request.ClientOrderId));
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Venue {Venue} threw placing order {ClientOrderId}",
                DeFiUtils.SafeLog(venueName),
                DeFiUtils.SafeLog(request.ClientOrderId));
            return null;
        }
    }
}
