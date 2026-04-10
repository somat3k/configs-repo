using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MLS.Arbitrager.Configuration;
using MLS.Arbitrager.Scanning;
using MLS.Arbitrager.Scoring;
using Xunit;

namespace MLS.Arbitrager.Tests;

/// <summary>
/// Unit tests for <see cref="OpportunityScorer"/> — rule-based confidence scoring.
/// </summary>
public sealed class OpportunityScorerTests : IDisposable
{
    private readonly OpportunityScorer _scorer;

    public OpportunityScorerTests()
    {
        var opts = Options.Create(new ArbitragerOptions
        {
            ModelPath          = string.Empty, // force rule-based
            MinScorerConfidence = 0.6f,
        });
        _scorer = new OpportunityScorer(opts, NullLogger<OpportunityScorer>.Instance);
    }

    public void Dispose() => _scorer.Dispose();

    private static ArbitrageOpportunity MakeOpportunity(
        decimal netProfitUsd, decimal profitRatio, int hopCount, decimal gasUsd)
    {
        var hops = Enumerable.Range(0, hopCount)
            .Select(i => new ArbHopDetail($"T{i}", $"T{i + 1}", "camelot", 1m, 0.003m, gasUsd / hopCount))
            .ToList();

        var now = DateTimeOffset.UtcNow;
        return new ArbitrageOpportunity(
            OpportunityId:      Guid.NewGuid(),
            Hops:               hops.AsReadOnly(),
            InputAmountUsd:     1_000m,
            EstimatedOutputUsd: 1_000m + netProfitUsd + gasUsd,
            GasEstimateUsd:     gasUsd,
            NetProfitUsd:       netProfitUsd,
            ProfitRatio:        profitRatio,
            DetectedAt:         now,
            ExpiresAt:          now.AddSeconds(2));
    }

    [Fact]
    public async Task ScoreAsync_ReturnsValueBetweenZeroAndOne()
    {
        var opp   = MakeOpportunity(50m, 0.05m, 2, 0.1m);
        var score = await _scorer.ScoreAsync(opp, CancellationToken.None);
        score.Should().BeInRange(0f, 1f);
    }

    [Fact]
    public async Task ScoreAsync_HighProfitRatioYieldsHigherScore()
    {
        var lowProfit  = MakeOpportunity(1m,   0.001m, 2, 0.05m);
        var highProfit = MakeOpportunity(50m,  0.05m,  2, 0.05m);

        var lowScore  = await _scorer.ScoreAsync(lowProfit,  CancellationToken.None);
        var highScore = await _scorer.ScoreAsync(highProfit, CancellationToken.None);

        highScore.Should().BeGreaterThan(lowScore);
    }

    [Fact]
    public async Task ScoreAsync_MoreHopsReducesScore()
    {
        var twoHops  = MakeOpportunity(10m, 0.01m, 2, 0.1m);
        var fourHops = MakeOpportunity(10m, 0.01m, 4, 0.1m);

        var scoreTwo  = await _scorer.ScoreAsync(twoHops,  CancellationToken.None);
        var scoreFour = await _scorer.ScoreAsync(fourHops, CancellationToken.None);

        scoreTwo.Should().BeGreaterThan(scoreFour);
    }

    [Fact]
    public async Task ScoreAsync_NegativeProfitYieldsLowScore()
    {
        var badOpp = MakeOpportunity(-5m, -0.005m, 2, 10m);
        var score  = await _scorer.ScoreAsync(badOpp, CancellationToken.None);
        score.Should().BeLessThan(0.5f);
    }

    [Theory]
    [InlineData(2, 0.05f,  0.6f, true)]   // good opportunity
    [InlineData(4, 0.001f, 0.6f, false)]  // marginal — many hops, low profit
    public async Task ScoreAsync_ThresholdDecision(int hopCount, float profitRatio, float threshold, bool shouldPass)
    {
        var opp   = MakeOpportunity((decimal)profitRatio * 1000m, (decimal)profitRatio, hopCount, 0.1m);
        var score = await _scorer.ScoreAsync(opp, CancellationToken.None);

        if (shouldPass)
            score.Should().BeGreaterThanOrEqualTo(threshold);
        else
            score.Should().BeLessThan(threshold);
    }
}
