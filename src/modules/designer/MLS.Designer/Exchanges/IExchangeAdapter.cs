using MLS.Core.Constants;
using MLS.Core.Designer;

namespace MLS.Designer.Exchanges;

/// <summary>
/// Normalised exchange adapter interface — all DEX and CEX integrations implement this contract.
/// Each adapter is responsible for a single exchange (one class per exchange).
/// All blockchain addresses must be resolved via <see cref="IBlockchainAddressBook"/>.
/// Adapters implement exponential backoff on connection failure (base 1s, max 60s, with jitter).
/// </summary>
public interface IExchangeAdapter : IAsyncDisposable
{
    /// <summary>Unique exchange key, e.g. <c>"camelot"</c>, <c>"hyperliquid"</c>, <c>"dfyn"</c>.</summary>
    string ExchangeId { get; }

    /// <summary>
    /// Get current best mid-price for a token pair.
    /// Target latency: &lt; 100ms (backed by a 1-second Redis/in-memory cache).
    /// </summary>
    /// <param name="baseToken">Base token symbol (e.g. <c>"WETH"</c>).</param>
    /// <param name="quoteToken">Quote token symbol (e.g. <c>"USDC"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<decimal> GetPriceAsync(string baseToken, string quoteToken, CancellationToken ct);

    /// <summary>
    /// Execute a swap. Slippage is validated before submitting the on-chain transaction.
    /// Throws <see cref="SlippageExceededException"/> if market moved beyond tolerance.
    /// </summary>
    Task<SwapResult> ExecuteSwapAsync(SwapRequest request, CancellationToken ct);

    /// <summary>
    /// Get an order book depth snapshot for slippage estimation.
    /// </summary>
    /// <param name="symbol">Token pair symbol (e.g. <c>"WETH/USDC"</c>).</param>
    /// <param name="depth">Number of price levels on each side.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OrderBookSnapshot> GetOrderBookAsync(string symbol, int depth, CancellationToken ct);

    /// <summary>
    /// Subscribe to a live price stream.
    /// Yields <see cref="PriceUpdate"/> ticks until <paramref name="ct"/> is cancelled.
    /// Reconnects automatically with exponential backoff on disconnection.
    /// </summary>
    IAsyncEnumerable<PriceUpdate> SubscribePriceStreamAsync(string symbol, CancellationToken ct);

    /// <summary>
    /// Check liveness. Returns <c>false</c> if the exchange is currently unreachable.
    /// </summary>
    Task<bool> CheckAvailabilityAsync(CancellationToken ct);
}
