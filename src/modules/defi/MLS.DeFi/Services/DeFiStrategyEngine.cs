using MLS.DeFi.Interfaces;
using MLS.DeFi.Models;

namespace MLS.DeFi.Services;

/// <summary>
/// Selects and executes the optimal DeFi strategy for a given trade request.
/// Preference order for perpetual futures: HYPERLIQUID (primary) → fallback chain.
/// Spot swaps: Camelot → DFYN → Balancer.
/// Lending: Morpho.
/// Multi-hop routing: nHOP.
/// Uniswap is strictly forbidden.
/// </summary>
public sealed class DeFiStrategyEngine(
    IHyperliquidClient _hyperliquid,
    IBrokerFallbackChain _fallbackChain,
    ILogger<DeFiStrategyEngine> _logger) : IDeFiStrategyEngine
{
    // Basis-point fees per venue (approximate; used for strategy selection)
    private const int HyperliquidFeesBps = 2;   // 0.02% taker
    private const int CamelotFeesBps     = 30;  // 0.30% default pool
    private const int DfynFeesBps        = 25;  // 0.25%
    private const int BalancerFeesBps    = 10;  // variable; 0.10% typical
    private const int MorphoFeesBps      = 5;   // supply/borrow
    private const int NHopFeesBps        = 35;  // multi-hop aggregate

    /// <inheritdoc/>
    public async Task<DeFiStrategyResult> EvaluateAsync(DeFiStrategyRequest request, CancellationToken ct)
    {
        var strategy = SelectStrategy(request);
        var feesBps  = EstimateFees(strategy);
        var output   = EstimateOutput(request.Quantity, feesBps, request.MaxSlippageBps);

        _logger.LogInformation(
            "Strategy evaluation: symbol={Symbol} side={Side} qty={Qty} strategy={Strategy} venue={Venue} feesBps={Fees}",
            DeFiUtils.SafeLog(request.Symbol), request.Side, request.Quantity, strategy.StrategyType, strategy.Venue, feesBps);

        // Check HYPERLIQUID availability for perpetual strategy
        if (strategy.StrategyType == DeFiStrategyType.HyperliquidPerpetual)
        {
            try
            {
                // Connectivity check — result is unused; exception signals unavailability
                await _hyperliquid.GetOpenOrdersAsync(string.Empty, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "HYPERLIQUID connectivity check failed during strategy evaluation");
            }
        }

        return strategy with
        {
            EstimatedOutputQuantity = output,
            EstimatedFeesBps        = feesBps,
        };
    }

    /// <inheritdoc/>
    public async Task<DeFiStrategyResult> ExecuteAsync(DeFiStrategyRequest request, CancellationToken ct)
    {
        var strategy = await EvaluateAsync(request, ct).ConfigureAwait(false);

        if (strategy.ExecutionOrder is null)
        {
            _logger.LogWarning("Strategy {Strategy} produced no execution order — skipping",
                strategy.StrategyType);
            return strategy;
        }

        DeFiOrderResult result;
        try
        {
            result = await _fallbackChain.ExecuteWithFallbackAsync(strategy.ExecutionOrder, ct)
                                         .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "All venues failed for strategy {Strategy}", strategy.StrategyType);
            throw;
        }

        _logger.LogInformation("Strategy {Strategy} executed: clientOrderId={Id} state={State}",
            strategy.StrategyType,
            DeFiUtils.SafeLog(result.ClientOrderId),
            result.State);

        return strategy with { ExecutedOrderResult = result };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAvailableVenuesAsync(CancellationToken ct)
    {
        var venues  = new List<string>();
        var brokers = await _fallbackChain.GetActiveBrokersAsync(ct).ConfigureAwait(false);
        venues.AddRange(brokers);

        // Static list of protocol venues always considered available (network-level health
        // checks are out of scope for this version; on-chain calls determine liveness)
        venues.Add("camelot");
        venues.Add("dfyn");
        venues.Add("balancer");
        venues.Add("morpho");
        venues.Add("nhop");

        return venues.AsReadOnly();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static DeFiStrategyResult SelectStrategy(DeFiStrategyRequest request)
    {
        // Default: HYPERLIQUID perpetual (lowest fees, primary per architectural rules)
        var strategy   = DeFiStrategyType.HyperliquidPerpetual;
        var venue      = "hyperliquid";

        // If slippage tolerance is very tight and qty is large, prefer Camelot spot
        if (request.MaxSlippageBps < 10 && request.Quantity > 10m)
        {
            strategy = DeFiStrategyType.CamelotSpotSwap;
            venue    = "camelot";
        }

        var execOrder = new DeFiOrderRequest(
            request.Symbol,
            request.Side,
            DeFiOrderType.Market,
            request.Quantity,
            null,
            null,
            Guid.NewGuid().ToString(),
            request.RequestingModuleId);

        return new DeFiStrategyResult(strategy, venue, 0m, 0, execOrder);
    }

    private static int EstimateFees(DeFiStrategyResult strategy) => strategy.StrategyType switch
    {
        DeFiStrategyType.HyperliquidPerpetual => HyperliquidFeesBps,
        DeFiStrategyType.CamelotSpotSwap      => CamelotFeesBps,
        DeFiStrategyType.DfynSpotSwap         => DfynFeesBps,
        DeFiStrategyType.BalancerPool         => BalancerFeesBps,
        DeFiStrategyType.MorphoLending        => MorphoFeesBps,
        DeFiStrategyType.NHopRoute            => NHopFeesBps,
        _                                     => HyperliquidFeesBps,
    };

    private static decimal EstimateOutput(decimal quantity, int feesBps, int maxSlippageBps)
    {
        var totalDeductionBps = feesBps + maxSlippageBps;
        var deduction         = quantity * totalDeductionBps / 10_000m;
        return quantity - deduction;
    }
}
