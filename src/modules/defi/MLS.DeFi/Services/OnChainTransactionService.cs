using System.Net.Http.Json;
using System.Text.Json;
using MLS.DeFi.Addresses;
using MLS.DeFi.Configuration;
using MLS.DeFi.Interfaces;
using MLS.DeFi.Models;

namespace MLS.DeFi.Services;

/// <summary>
/// Broadcasts signed transactions to the configured EVM-compatible chain via JSON-RPC
/// (<c>eth_sendRawTransaction</c>) and polls for confirmation (<c>eth_getTransactionReceipt</c>).
/// </summary>
/// <remarks>
/// All contract addresses are resolved from PostgreSQL via <see cref="IDeFiAddressBook"/>.
/// No addresses are hardcoded.
/// </remarks>
public sealed class OnChainTransactionService(
    IHttpClientFactory _httpFactory,
    IDeFiAddressBook _addressBook,
    IWalletProvider _wallet,
    IOptions<DeFiOptions> _options,
    ILogger<OnChainTransactionService> _logger) : IOnChainTransactionService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc/>
    public async Task<OnChainTransactionResult> SubmitAsync(
        OnChainTransactionRequest request,
        CancellationToken ct)
    {
        // 1. Resolve contract address from PostgreSQL
        var toAddress = await _addressBook.GetAddressAsync(request.AddressName, ct)
                                          .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            _logger.LogError("Address '{AddressName}' not found in blockchain_addresses table",
                DeFiUtils.SafeLog(request.AddressName));
            return new OnChainTransactionResult(null, OnChainTxStatus.Failed, 0, 0, DateTimeOffset.UtcNow);
        }

        // 2. Sign the transaction via wallet provider
        WalletSignResult signed;
        try
        {
            signed = await _wallet.SignTransactionAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Transaction signing failed for address={AddressName}",
                DeFiUtils.SafeLog(request.AddressName));
            return new OnChainTransactionResult(null, OnChainTxStatus.Failed, 0, 0, DateTimeOffset.UtcNow);
        }

        // 3. Broadcast via JSON-RPC
        var rpcUrl = _options.Value.ChainRpcUrl;
        var client = _httpFactory.CreateClient("chain-rpc");

        var rpcBody = new
        {
            jsonrpc = "2.0",
            id      = 1,
            method  = "eth_sendRawTransaction",
            @params = new[] { signed.SignedTxHex },
        };

        try
        {
            var response = await client.PostAsJsonAsync(rpcUrl, rpcBody, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            var txHash = doc.RootElement.TryGetProperty("result", out var r)
                ? r.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(txHash))
            {
                var errorMsg = doc.RootElement.TryGetProperty("error", out var err)
                    ? err.ToString()
                    : "unknown RPC error";
                _logger.LogError("eth_sendRawTransaction returned error: {Error}", errorMsg);
                return new OnChainTransactionResult(null, OnChainTxStatus.Failed, 0, 0, DateTimeOffset.UtcNow);
            }

            _logger.LogInformation("Transaction broadcast: txHash={TxHash} address={AddressName}",
                DeFiUtils.SafeLog(txHash), DeFiUtils.SafeLog(request.AddressName));

            return new OnChainTransactionResult(txHash, OnChainTxStatus.Pending, 0, 0, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "eth_sendRawTransaction call failed for address={AddressName}",
                DeFiUtils.SafeLog(request.AddressName));
            return new OnChainTransactionResult(null, OnChainTxStatus.Failed, 0, 0, DateTimeOffset.UtcNow);
        }
    }

    /// <inheritdoc/>
    public async Task<OnChainTransactionResult?> GetStatusAsync(string txHash, CancellationToken ct)
    {
        var rpcUrl = _options.Value.ChainRpcUrl;
        var client = _httpFactory.CreateClient("chain-rpc");

        var rpcBody = new
        {
            jsonrpc = "2.0",
            id      = 1,
            method  = "eth_getTransactionReceipt",
            @params = new[] { txHash },
        };

        try
        {
            var response = await client.PostAsJsonAsync(rpcUrl, rpcBody, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            if (!doc.RootElement.TryGetProperty("result", out var result) ||
                result.ValueKind == JsonValueKind.Null)
            {
                // Not yet mined
                return new OnChainTransactionResult(txHash, OnChainTxStatus.Pending, 0, 0, DateTimeOffset.UtcNow);
            }

            var statusHex  = result.TryGetProperty("status",      out var s)  ? s.GetString() : null;
            var gasUsedHex = result.TryGetProperty("gasUsed",     out var g)  ? g.GetString() : "0x0";
            var blockHex   = result.TryGetProperty("blockNumber", out var bn) ? bn.GetString() : "0x0";

            // status "0x1" = success (confirmed); "0x0" or missing = reverted
            var status = statusHex switch
            {
                "0x1"            => OnChainTxStatus.Confirmed,
                "0x0"            => OnChainTxStatus.Reverted,
                null or ""       => OnChainTxStatus.Failed,
                _                => OnChainTxStatus.Reverted,
            };
            var gasUsed   = ParseHexUlong(gasUsedHex);
            var blockNum  = ParseHexUlong(blockHex);

            return new OnChainTransactionResult(txHash, status, gasUsed, blockNum, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "eth_getTransactionReceipt failed for txHash={TxHash}",
                DeFiUtils.SafeLog(txHash));
            return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static ulong ParseHexUlong(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return 0;
        var clean = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
        return ulong.TryParse(clean, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
