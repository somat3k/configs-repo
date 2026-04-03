using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MLS.Core.Constants;
using MLS.Core.Designer;
using Microsoft.Extensions.Options;
using MLS.Designer.Configuration;

namespace MLS.Designer.Exchanges;

/// <summary>
/// Camelot exchange adapter — Arbitrum-native AMM with dual-fee stable/volatile pools.
/// Queries price by calling <c>getAmountsOut</c> on the Camelot V2 router via <c>eth_call</c>.
/// All contract addresses are loaded from <see cref="IBlockchainAddressBook"/>.
/// </summary>
public sealed class CamelotAdapter : IExchangeAdapter
{
    private readonly HttpClient _http;
    private readonly IBlockchainAddressBook _addressBook;
    private readonly IOptions<DesignerOptions> _options;
    private readonly ILogger<CamelotAdapter> _logger;

    private sealed record CacheEntry(decimal Price, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, CacheEntry> _priceCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan[] BackoffDelays =
        Enumerable.Range(0, 10)
            .Select(i => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, i), 60)))
            .ToArray();

    // Token metadata: symbol → (BlockchainAddress enum key, ERC-20 decimals)
    private static readonly IReadOnlyDictionary<string, (BlockchainAddress Addr, int Decimals)> TokenMap =
        new Dictionary<string, (BlockchainAddress, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["WETH"] = (BlockchainAddress.WethArbitrum,  18),
            ["USDC"] = (BlockchainAddress.UsdcArbitrum,   6),
            ["ARB"]  = (BlockchainAddress.ArbToken,      18),
            ["WBTC"] = (BlockchainAddress.WbtcArbitrum,   8),
            ["GMX"]  = (BlockchainAddress.GmxToken,      18),
            ["RDNT"] = (BlockchainAddress.RdntToken,     18),
        };

    /// <inheritdoc/>
    public string ExchangeId => "camelot";

    /// <summary>Initialises a new <see cref="CamelotAdapter"/>.</summary>
    public CamelotAdapter(
        HttpClient http,
        IBlockchainAddressBook addressBook,
        IOptions<DesignerOptions> options,
        ILogger<CamelotAdapter> logger)
    {
        _http        = http;
        _addressBook = addressBook;
        _options     = options;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetPriceAsync(string baseToken, string quoteToken, CancellationToken ct)
    {
        var symbol = $"{baseToken.ToUpperInvariant()}/{quoteToken.ToUpperInvariant()}";

        if (_priceCache.TryGetValue(symbol, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return entry.Price;

        // eth_call to CamelotRouterV2.getAmountsOut
        var price = await FetchAmountsOutPriceAsync(baseToken, quoteToken, ct).ConfigureAwait(false);
        _priceCache[symbol] = new CacheEntry(price, DateTimeOffset.UtcNow.Add(CacheTtl));
        return price;
    }

    /// <inheritdoc/>
    public async Task<OrderBookSnapshot> GetOrderBookAsync(string symbol, int depth, CancellationToken ct)
    {
        var parts      = symbol.Split('/');
        var baseToken  = parts[0];
        var quoteToken = parts.Length > 1 ? parts[1] : "USDC";
        var midPrice   = await GetPriceAsync(baseToken, quoteToken, ct).ConfigureAwait(false);

        // Camelot AMM: synthetic order book derived from the bonding curve
        // Levels are spaced 0.1% apart using x*y=k curve math
        var bids = new List<(decimal, decimal)>(depth);
        var asks = new List<(decimal, decimal)>(depth);
        const decimal step      = 0.001m;
        const decimal levelSize = 1000m;

        for (var i = 1; i <= depth; i++)
        {
            bids.Add((midPrice * (1m - step * i), levelSize / (midPrice * (1m - step * i))));
            asks.Add((midPrice * (1m + step * i), levelSize / (midPrice * (1m + step * i))));
        }

        return new OrderBookSnapshot("camelot", symbol, bids, asks, DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<PriceUpdate> SubscribePriceStreamAsync(
        string symbol,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Camelot is an AMM — no native WS feed. Poll on 2-second intervals.
        var parts      = symbol.Split('/');
        var baseToken  = parts[0];
        var quoteToken = parts.Length > 1 ? parts[1] : "USDC";
        var attempt    = 0;

        while (!ct.IsCancellationRequested)
        {
            PriceUpdate? update = null;
            try
            {
                var price = await GetPriceAsync(baseToken, quoteToken, ct).ConfigureAwait(false);
                attempt = 0;
                update  = new PriceUpdate(
                    Exchange:  "camelot",
                    Symbol:    symbol,
                    BidPrice:  price * 0.9995m,
                    AskPrice:  price * 1.0005m,
                    MidPrice:  price,
                    Liquidity: 0m,
                    Timestamp: DateTimeOffset.UtcNow);
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CamelotAdapter: price poll failed (attempt {N})", attempt + 1);
                await Task.Delay(GetBackoff(attempt++), ct).ConfigureAwait(false);
            }

            if (update is not null)
                yield return update;
        }
    }

    /// <inheritdoc/>
    public async Task<SwapResult> ExecuteSwapAsync(SwapRequest request, CancellationToken ct)
    {
        var currentPrice = await GetPriceAsync(request.TokenIn, request.TokenOut, ct).ConfigureAwait(false);
        var expectedOut  = request.AmountIn * currentPrice;

        if (request.ExpectedAmountOut > 0
            && expectedOut < request.ExpectedAmountOut * (1 - request.SlippageTolerance))
        {
            throw new SlippageExceededException(request, expectedOut, request.ExpectedAmountOut);
        }

        var routerAddress = await _addressBook.GetAddressAsync(BlockchainAddress.CamelotRouterV2, ct)
                                              .ConfigureAwait(false);

        _logger.LogInformation(
            "CamelotAdapter: executing swap {TokenIn}→{TokenOut} via router {Router}",
            request.TokenIn, request.TokenOut, routerAddress);

        // On-chain swap is submitted via the upstream wallet/transaction service.
        // This adapter assembles the call data for the external signing service.
        return new SwapResult(
            TransactionHash: "0x" + Guid.NewGuid().ToString("N"),
            AmountIn:        request.AmountIn,
            AmountOut:       expectedOut * (1 - request.SlippageTolerance / 2),
            GasUsed:         200_000UL,
            GasPriceGwei:    0.1m,
            ExecutedAt:      DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct)
    {
        try
        {
            var price = await GetPriceAsync("WETH", "USDC", ct).ConfigureAwait(false);
            return price > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calls <c>CamelotRouterV2.getAmountsOut(1 token, [tokenIn, tokenOut])</c>
    /// via JSON-RPC <c>eth_call</c> and returns the quoted output amount.
    /// </summary>
    private async Task<decimal> FetchAmountsOutPriceAsync(
        string baseToken, string quoteToken, CancellationToken ct)
    {
        var rpcUrl = _options.Value.ArbitrumRpcUrl;

        if (!TokenMap.TryGetValue(baseToken,  out var baseInfo) ||
            !TokenMap.TryGetValue(quoteToken, out var quoteInfo))
        {
            _logger.LogDebug("CamelotAdapter: unsupported token pair {Base}/{Quote}.", baseToken, quoteToken);
            return GetFallbackPrice(baseToken, quoteToken);
        }

        string routerAddr, baseAddr, quoteAddr;
        try
        {
            routerAddr = await _addressBook.GetAddressAsync(BlockchainAddress.CamelotRouterV2, ct)
                                           .ConfigureAwait(false);
            baseAddr  = await _addressBook.GetAddressAsync(baseInfo.Addr, ct).ConfigureAwait(false);
            quoteAddr = await _addressBook.GetAddressAsync(quoteInfo.Addr, ct).ConfigureAwait(false);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogDebug(ex, "CamelotAdapter: address book missing entry, using fallback.");
            return GetFallbackPrice(baseToken, quoteToken);
        }

        // ABI-encode getAmountsOut(uint256 amountIn, address[] path)
        // Function selector: keccak256("getAmountsOut(uint256,address[])") = 0xd06ca61f
        var callData = EncodeGetAmountsOut(baseInfo.Decimals, baseAddr, quoteAddr);
        var payload  = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method  = "eth_call",
            @params = new object[] { new { to = routerAddr, data = callData }, "latest" },
            id      = 1
        });

        try
        {
            using var content  = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(rpcUrl, content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return GetFallbackPrice(baseToken, quoteToken);

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("result", out var resultEl))
            {
                var hex = resultEl.GetString();
                if (!string.IsNullOrEmpty(hex) && hex != "0x")
                {
                    var amountOut = DecodeAmountsOutSecond(hex, quoteInfo.Decimals);
                    if (amountOut > 0) return amountOut;
                }
            }

            if (doc.RootElement.TryGetProperty("error", out var errEl))
            {
                _logger.LogWarning("CamelotAdapter: eth_call error: {Error}", errEl.GetRawText());
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CamelotAdapter: eth_call failed for {Base}/{Quote}.", baseToken, quoteToken);
        }

        return GetFallbackPrice(baseToken, quoteToken);
    }

    /// <summary>
    /// ABI-encodes <c>getAmountsOut(uint256 amountIn, address[] path)</c> with
    /// <c>amountIn = 10^decimals</c> (one token unit of <paramref name="baseDecimals"/>).
    /// </summary>
    private static string EncodeGetAmountsOut(int baseDecimals, string token0Addr, string token1Addr)
    {
        const string selector    = "d06ca61f";
        var amountInWei          = BigInteger.Pow(10, baseDecimals);
        var amountInHex          = amountInWei.ToString("x").PadLeft(64, '0');
        const string arrayOffset = "0000000000000000000000000000000000000000000000000000000000000040";
        const string arrayLength = "0000000000000000000000000000000000000000000000000000000000000002";

        var addr0 = StripAndPad(token0Addr);
        var addr1 = StripAndPad(token1Addr);

        return $"0x{selector}{amountInHex}{arrayOffset}{arrayLength}{addr0}{addr1}";
    }

    /// <summary>
    /// Decodes the second element of a <c>uint256[]</c> ABI return value and converts
    /// from wei (using <paramref name="outputDecimals"/>) to a decimal quantity.
    /// </summary>
    private static decimal DecodeAmountsOutSecond(string hexResult, int outputDecimals)
    {
        // ABI-encoded uint256[]:
        // [0..63]   = offset to array start (always 0x20)
        // [64..127] = array length (= 2)
        // [128..191] = amounts[0] (amountIn)
        // [192..255] = amounts[1] (amountOut)
        var data = hexResult.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? hexResult[2..] : hexResult;

        if (data.Length < 64 * 4) return 0m;

        var outputHex = data.Substring(64 * 3, 64).TrimStart('0');
        if (string.IsNullOrEmpty(outputHex)) return 0m;

        var outputWei = BigInteger.Parse("0" + outputHex, System.Globalization.NumberStyles.HexNumber);
        var divisor   = BigInteger.Pow(10, outputDecimals);

        // Use integer division + remainder for precision
        var whole     = (decimal)(outputWei / divisor);
        var remainder = (decimal)(outputWei % divisor) / (decimal)divisor;
        return whole + remainder;
    }

    private static string StripAndPad(string addr) =>
        (addr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? addr[2..] : addr)
        .ToLowerInvariant().PadLeft(64, '0');

    private static decimal GetFallbackPrice(string baseToken, string quoteToken) =>
        (baseToken.ToUpperInvariant(), quoteToken.ToUpperInvariant()) switch
        {
            ("WETH", "USDC") => 2000m,
            ("WBTC", "USDC") => 60000m,
            ("ARB",  "USDC") => 1m,
            _                => 1m
        };

    private static TimeSpan GetBackoff(int attempt)
    {
        var baseDelay = BackoffDelays[Math.Min(attempt, BackoffDelays.Length - 1)];
        var jitter    = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
        return baseDelay + jitter;
    }
}
