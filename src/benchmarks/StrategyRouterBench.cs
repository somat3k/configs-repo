using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using MLS.BlockController.Services;
using MLS.Core.Contracts;

namespace MLS.Benchmarks;

/// <summary>
/// Benchmarks for the strategy-router hot path: subscription table management
/// and strategy-graph deployment.
/// <para>
/// Performance targets:
/// <list type="bullet">
///   <item>Subscription lookup: &lt; 200ns median (p50)</item>
///   <item>Strategy deploy (100 blocks): &lt; 5ms median (p50)</item>
/// </list>
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
public class StrategyRouterBench
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private SubscriptionTable _subscriptionTable = null!;
    private StrategyRouter    _router            = null!;

    private string _hotTopic = null!;

    /// <summary>Graph with 100 blocks connected linearly (maximum test load).</summary>
    private MLS.Core.Contracts.Designer.StrategyGraphPayload _graph100 = null!;
    private MLS.Core.Contracts.Designer.StrategyGraphPayload _graph10  = null!;

    // ── Setup ─────────────────────────────────────────────────────────────────

    [GlobalSetup]
    public void Setup()
    {
        _subscriptionTable = new SubscriptionTable();
        _router            = new StrategyRouter(
            _subscriptionTable,
            new NoOpRouter(),
            NullLogger<StrategyRouter>.Instance);

        // Pre-populate subscription table with 1 000 entries for a realistic lookup bench
        var strategyId = Guid.NewGuid();
        for (int i = 0; i < 1_000; i++)
        {
            var topic = StrategyRouter.BuildTopic(strategyId, Guid.NewGuid(), $"output_{i}");
            // SubscriptionTable.AddAsync is implemented synchronously (returns ValueTask.CompletedTask).
            // GlobalSetup is a synchronous method so we drain the completed ValueTask here.
            // This is the correct pattern for consuming a synchronously-completed ValueTask in
            // a non-async context; no thread-pool blocking occurs.
            _subscriptionTable.AddAsync(topic, Guid.NewGuid()).GetAwaiter().GetResult();
            if (i == 500) _hotTopic = topic;
        }

        _graph10  = BuildGraph(10);
        _graph100 = BuildGraph(100);
    }

    // ── Benchmarks ────────────────────────────────────────────────────────────

    /// <summary>
    /// O(1) subscription lookup in a pre-populated table.
    /// Target: &lt; 200ns median.
    /// </summary>
    [Benchmark(Description = "Subscription lookup O(1) — 1 000-entry table, hot topic")]
    public IReadOnlySet<Guid> SubscriptionLookup() =>
        _subscriptionTable.GetSubscribers(_hotTopic);

    /// <summary>
    /// Topic key construction: <c>StrategyRouter.BuildTopic</c>.
    /// Verifies the string interpolation has no hidden alloc surprises under load.
    /// </summary>
    [Benchmark(Description = "BuildTopic — string format Guid/Guid/socketName")]
    public string BuildTopic() =>
        StrategyRouter.BuildTopic(Guid.NewGuid(), Guid.NewGuid(), "signal_out");

    /// <summary>
    /// Strategy deploy with a 10-block graph — baseline for small strategies.
    /// </summary>
    [Benchmark(Description = "DeployAsync — 10-block graph")]
    public async Task DeployGraph10() =>
        await _router.DeployAsync(_graph10, CancellationToken.None).ConfigureAwait(false);

    /// <summary>
    /// Strategy deploy with a 100-block graph — stress test for large compositions.
    /// Target: &lt; 5ms median.
    /// </summary>
    [Benchmark(Description = "DeployAsync — 100-block graph (TARGET < 5ms)")]
    public async Task DeployGraph100() =>
        await _router.DeployAsync(_graph100, CancellationToken.None).ConfigureAwait(false);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MLS.Core.Contracts.Designer.StrategyGraphPayload BuildGraph(int blockCount)
    {
        var blocks = new List<MLS.Core.Contracts.Designer.StrategyBlockSpec>(blockCount);
        var connections = new List<MLS.Core.Contracts.Designer.StrategyConnectionSpec>(blockCount - 1);

        var ids = new Guid[blockCount];
        for (int i = 0; i < blockCount; i++)
            ids[i] = Guid.NewGuid();

        for (int i = 0; i < blockCount; i++)
        {
            blocks.Add(new MLS.Core.Contracts.Designer.StrategyBlockSpec(
                ids[i],
                $"Block{i}",
                System.Text.Json.JsonSerializer.SerializeToElement(new { period = 14 })));
        }

        for (int i = 0; i < blockCount - 1; i++)
        {
            connections.Add(new MLS.Core.Contracts.Designer.StrategyConnectionSpec(
                ConnectionId: $"conn_{i}",
                FromBlockId:  ids[i],
                FromSocket:   "signal_out",
                ToBlockId:    ids[i + 1],
                ToSocket:     "signal_in"));
        }

        return new MLS.Core.Contracts.Designer.StrategyGraphPayload(
            GraphId:       Guid.NewGuid(),
            Name:          $"Bench-{blockCount}-Blocks",
            SchemaVersion: 1,
            Blocks:        blocks,
            Connections:   connections);
    }

    // ── No-op IMessageRouter for benchmark isolation ──────────────────────────

    private sealed class NoOpRouter : IMessageRouter
    {
        public Task RouteAsync(EnvelopePayload envelope, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task BroadcastAsync(EnvelopePayload envelope, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
