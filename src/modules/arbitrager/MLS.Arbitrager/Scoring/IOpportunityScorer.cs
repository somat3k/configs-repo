namespace MLS.Arbitrager.Scoring;

/// <summary>
/// Scores an <see cref="Scanning.ArbitrageOpportunity"/> and returns a confidence in [0, 1].
/// Uses the ONNX model-a when available, otherwise falls back to a rule-based heuristic scorer.
/// </summary>
public interface IOpportunityScorer
{
    /// <summary>
    /// Scores the given opportunity.
    /// Returns a confidence value in [0, 1] — higher is better.
    /// </summary>
    /// <param name="opportunity">The candidate opportunity to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<float> ScoreAsync(Scanning.ArbitrageOpportunity opportunity, CancellationToken ct);
}
