using MLS.Arbitrager.Configuration;
using MLS.Arbitrager.Scanning;
using MLS.Arbitrager.Scoring;
using MLS.Arbitrager.Services;
using MLS.Core.Constants;
using MLS.Core.Contracts;

namespace MLS.Arbitrager.Execution;

/// <summary>
/// Concrete arbitrage executor — scores the opportunity, builds a transaction array
/// when confidence is sufficient, and sends <c>ARB_PATH_FOUND</c> + dispatches the
/// array to the Transactions module via the Block Controller envelope bus.
/// </summary>
public sealed class ArbitrageExecutor(
    IOpportunityScorer _scorer,
    IArrayBuilder _builder,
    IEnvelopeSender _sender,
    IOptions<ArbitragerOptions> _options,
    ILogger<ArbitrageExecutor> _logger) : IArbitrageExecutor
{
    private const string ModuleId = "arbitrager";

    /// <inheritdoc/>
    public async Task ExecuteAsync(ArbitrageOpportunity opportunity, CancellationToken ct)
    {
        if (opportunity.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _logger.LogDebug("Opportunity {Id} already expired — skipping.", opportunity.OpportunityId);
            return;
        }

        // 1. Score
        var confidence = await _scorer.ScoreAsync(opportunity, ct).ConfigureAwait(false);

        _logger.LogDebug(
            "Opportunity {Id}: netProfit={Net:F4} USD, confidence={Conf:F3}",
            opportunity.OpportunityId, opportunity.NetProfitUsd, confidence);

        if (confidence < _options.Value.MinScorerConfidence)
        {
            _logger.LogDebug(
                "Opportunity {Id} scored below threshold ({Score:F3} < {Threshold:F3}) — discarding.",
                opportunity.OpportunityId, confidence, _options.Value.MinScorerConfidence);
            return;
        }

        // 2. Broadcast ARB_PATH_FOUND to Block Controller (designer + broker receive this)
        var arbPathPayload = new
        {
            path_id              = opportunity.OpportunityId,
            hops                 = opportunity.Hops.Select(h => new
            {
                from_token = h.FromToken,
                to_token   = h.ToToken,
                exchange   = h.Exchange,
                price      = h.Price,
            }).ToArray(),
            input_amount_usd     = opportunity.InputAmountUsd,
            estimated_output_usd = opportunity.EstimatedOutputUsd,
            gas_estimate_usd     = opportunity.GasEstimateUsd,
            net_profit_usd       = opportunity.NetProfitUsd,
            expires_at           = opportunity.ExpiresAt,
            scorer_confidence    = confidence,
        };

        var arbPathEnvelope = EnvelopePayload.Create(
            type:     MessageTypes.ArbPathFound,
            moduleId: ModuleId,
            payload:  arbPathPayload);

        await _sender.SendEnvelopeAsync(arbPathEnvelope, ct).ConfigureAwait(false);

        // 3. Build transaction array
        TransactionArray array;
        try
        {
            array = await _builder.BuildAsync(opportunity, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to build transaction array for opportunity {Id}.",
                opportunity.OpportunityId);
            return;
        }

        // 4. Dispatch ARBITRAGE_OPPORTUNITY envelope carrying the full transaction array
        //    The Transactions module subscribes to this message type.
        var dispatchEnvelope = EnvelopePayload.Create(
            type:     MessageTypes.ArbitrageOpportunity,
            moduleId: ModuleId,
            payload:  array);

        await _sender.SendEnvelopeAsync(dispatchEnvelope, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Dispatched array {ArrayId} for opportunity {OppId}: {Hops} hops, profit={Profit:F4} USD, confidence={Conf:F3}",
            array.ArrayId, opportunity.OpportunityId, array.Steps.Count,
            array.ExpectedNetProfitUsd, confidence);
    }
}
