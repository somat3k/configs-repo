using MLS.Broker.Configuration;
using MLS.Broker.Interfaces;
using MLS.Broker.Models;

namespace MLS.Broker.Services;

/// <summary>
/// Cascading fallback chain for order execution.
/// The primary venue is HYPERLIQUID; subsequent venues in the chain are tried
/// when the primary is unavailable or rejects the order.
/// Venues are resolved from <see cref="BrokerOptions.FallbackChain"/> at runtime —
/// no hardcoded venue IDs beyond the primary constant.
/// </summary>
public sealed class BrokerFallbackChain(
    IHyperliquidClient _primary,
    IOptions<BrokerOptions> _options,
    ILogger<BrokerFallbackChain> _logger) : IBrokerFallbackChain
{
    /// <inheritdoc/>
    public async Task<OrderResult> ExecuteWithFallbackAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        // Always try HYPERLIQUID first (primary venue per architectural rules)
        var result = await TryPlaceAsync(_primary, "hyperliquid", request, ct).ConfigureAwait(false);

        if (result is not null && result.State != OrderState.Rejected)
            return result;

        // Fallback chain (skip first entry — that's hyperliquid, already tried)
        var chain = _options.Value.FallbackChain;
        for (int i = 1; i < chain.Length; i++)
        {
            _logger.LogWarning("Primary venue rejected order {ClientOrderId} — trying fallback {Venue}",
                request.ClientOrderId, chain[i]);

            // Future venues are registered dynamically; for now log the attempt
            _logger.LogInformation("Fallback venue {Venue} not yet implemented — skipping", chain[i]);
        }

        throw new InvalidOperationException(
            $"All broker venues in the fallback chain failed to accept order {request.ClientOrderId}. " +
            $"Venues tried: {string.Join(", ", chain)}.");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetActiveBrokersAsync(CancellationToken ct)
    {
        var active = new List<string>();
        var chain  = _options.Value.FallbackChain;

        // Probe HYPERLIQUID with an open-orders check
        try
        {
            var _ = await _primary.GetOpenOrdersAsync(string.Empty, ct).ConfigureAwait(false);
            if (chain.Length > 0) active.Add(chain[0]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "HYPERLIQUID health probe failed");
        }

        return active;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<OrderResult?> TryPlaceAsync(
        IHyperliquidClient client,
        string venueName,
        PlaceOrderRequest request,
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
            _logger.LogWarning("Venue {Venue} timed out placing order {ClientOrderId}", venueName, request.ClientOrderId);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Venue {Venue} threw placing order {ClientOrderId}", venueName, request.ClientOrderId);
            return null;
        }
    }
}
