using MLS.Arbitrager.Addresses;
using MLS.Arbitrager.Scanning;
using MLS.Core.Constants;

namespace MLS.Arbitrager.Execution;

/// <summary>
/// Builds an ordered <see cref="TransactionArray"/> from a scored <see cref="ArbitrageOpportunity"/>.
/// Resolves router addresses from the blockchain address book; applies 0.5% slippage tolerance.
/// </summary>
public sealed class ArrayBuilder(
    IArbitragerAddressBook _addressBook,
    ILogger<ArrayBuilder> _logger) : IArrayBuilder
{
    private const decimal SlippageTolerance = 0.005m;  // 0.5%

    // Gas limit estimates per exchange type (Arbitrum is cheaper than Ethereum mainnet)
    private static long GasLimitFor(string exchange) => exchange.ToLowerInvariant() switch
    {
        "camelot"     => 300_000L,
        "dfyn"        => 300_000L,
        "balancer"    => 400_000L,
        "hyperliquid" => 250_000L,
        _             => 350_000L,
    };

    /// <inheritdoc/>
    public async ValueTask<TransactionArray> BuildAsync(
        ArbitrageOpportunity opportunity, CancellationToken ct)
    {
        var steps = new List<TransactionStep>(opportunity.Hops.Count);
        var runningAmount = opportunity.InputAmountUsd;

        for (var i = 0; i < opportunity.Hops.Count; i++)
        {
            var hop      = opportunity.Hops[i];
            var amountIn = runningAmount;

            // Resolve router address for this exchange
            var routerKey = RouterKeyFor(hop.Exchange);
            string routerAddress;

            try
            {
                routerAddress = await _addressBook.GetRouterAddressAsync(routerKey, ct)
                                                  .ConfigureAwait(false);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning(
                    "ArrayBuilder: no router address for exchange '{Exchange}' — using zero address.",
                    hop.Exchange);
                routerAddress = "0x0000000000000000000000000000000000000000";
            }

            var estimatedOut = amountIn * hop.Price * (1m - hop.Fee);
            var minAmountOut = estimatedOut * (1m - SlippageTolerance);

            steps.Add(new TransactionStep(
                SequenceIndex: i,
                FromToken:     hop.FromToken,
                ToToken:       hop.ToToken,
                AmountIn:      amountIn,
                MinAmountOut:  minAmountOut,
                Exchange:      hop.Exchange,
                RouterAddress: routerAddress,
                GasLimit:      GasLimitFor(hop.Exchange)));

            runningAmount = estimatedOut;
        }

        var now = DateTimeOffset.UtcNow;
        return new TransactionArray(
            ArrayId:                Guid.NewGuid(),
            OpportunityId:          opportunity.OpportunityId,
            Steps:                  steps.AsReadOnly(),
            InputAmountUsd:         opportunity.InputAmountUsd,
            ExpectedOutputUsd:      opportunity.EstimatedOutputUsd,
            ExpectedNetProfitUsd:   opportunity.NetProfitUsd,
            CreatedAt:              now,
            ExpiresAt:              opportunity.ExpiresAt);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static BlockchainAddress RouterKeyFor(string exchange) =>
        exchange.ToLowerInvariant() switch
        {
            "camelot"     => BlockchainAddress.CamelotRouterV2,
            "dfyn"        => BlockchainAddress.DfynRouter,
            "balancer"    => BlockchainAddress.BalancerVault,
            "hyperliquid" => BlockchainAddress.CamelotRouterV2, // Hyperliquid CEX — use Camelot for on-chain leg
            _             => BlockchainAddress.CamelotRouterV2,
        };
}
