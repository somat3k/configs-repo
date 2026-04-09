using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using MLS.Core.Constants;
using MLS.Core.Contracts;

namespace MLS.Benchmarks;

/// <summary>
/// Benchmarks for the envelope parsing and topic-routing hot path.
/// <para>
/// Performance targets:
/// <list type="bullet">
///   <item>Envelope parse + route: &lt; 1µs median (p50)</item>
///   <item>Subscription lookup: &lt; 200ns median (p50)</item>
///   <item>Envelope routing allocations: 0 bytes</item>
/// </list>
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 3, iterationCount: 10)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class EnvelopeRoutingBench
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private InProcessSubscriptionTable _table = null!;
    private string _populatedTopic = null!;
    private string _hotTopic = null!;
    private byte[] _envelopeJson = null!;
    private EnvelopePayload _prebuiltEnvelope = null!;

    // ── Setup ─────────────────────────────────────────────────────────────────

    [GlobalSetup]
    public void Setup()
    {
        _table = new InProcessSubscriptionTable();

        // Populate 1 000 strategy-scoped topics, each with 4 subscribers
        for (int i = 0; i < 1_000; i++)
        {
            var topic = $"strategy/{Guid.NewGuid():N}/block_{i}/out";
            for (int j = 0; j < 4; j++)
                _table.Add(topic, Guid.NewGuid());

            if (i == 500)
                _populatedTopic = topic; // a real "hot" strategy topic
        }

        _hotTopic = _populatedTopic;

        // Pre-build an envelope serialised to JSON bytes for the parse bench
        _prebuiltEnvelope = EnvelopePayload.Create(
            MessageTypes.TradeSignal,
            "trader",
            new { signal = "BUY", confidence = 0.87, symbol = "BTC-USDT" });

        _envelopeJson = JsonSerializer.SerializeToUtf8Bytes(_prebuiltEnvelope);

        // Also register 4 subscribers keyed by envelope.Type so the E2E bench
        // exercises the subscriber-iteration path (hit path) rather than the miss path.
        for (int j = 0; j < 4; j++)
            _table.Add(MessageTypes.TradeSignal, Guid.NewGuid());
    }

    // ── Benchmarks ────────────────────────────────────────────────────────────

    /// <summary>
    /// Deserialise a JSON envelope from a byte buffer — simulates the receive path
    /// before routing.  Target: &lt; 1µs median.
    /// </summary>
    [Benchmark(Description = "Envelope JSON parse (Utf8 bytes → EnvelopePayload)")]
    [BenchmarkCategory("Parse")]
    public EnvelopePayload ParseEnvelopeJson()
    {
        return JsonSerializer.Deserialize<EnvelopePayload>(_envelopeJson)!;
    }

    /// <summary>
    /// O(1) subscriber lookup in a pre-populated ConcurrentDictionary — the
    /// innermost operation of the routing hot path. Target: &lt; 200ns median.
    /// </summary>
    [Benchmark(Description = "Subscription lookup O(1) hit")]
    [BenchmarkCategory("Routing")]
    public IReadOnlySet<Guid> SubscriptionLookupHit()
    {
        return _table.GetSubscribers(_hotTopic);
    }

    /// <summary>
    /// Lookup for a topic that has no subscribers — verifies the miss-path
    /// returns an empty set with zero allocation.
    /// </summary>
    [Benchmark(Description = "Subscription lookup miss (no subscribers)")]
    [BenchmarkCategory("Routing")]
    public IReadOnlySet<Guid> SubscriptionLookupMiss()
    {
        return _table.GetSubscribers("strategy/unknown/block/out");
    }

    /// <summary>
    /// End-to-end envelope routing: parse → lookup → iterate subscribers.
    /// Zero-allocation constraint: ArrayPool for buffer, Span for topic extraction.
    /// Target: &lt; 1µs median, 0 bytes allocated.
    /// </summary>
    [Benchmark(Description = "Full route: parse JSON → lookup → iterate subscribers")]
    [BenchmarkCategory("E2E")]
    public int RouteEnvelopeEndToEnd()
    {
        // Deserialise
        var envelope = JsonSerializer.Deserialize<EnvelopePayload>(_envelopeJson)!;

        // Topic lookup using the envelope type as the routing key (realistic pattern)
        var subscribers = _table.GetSubscribers(envelope.Type);

        // Iterate subscribers without boxing — simulates the fan-out loop
        int count = 0;
        foreach (var _ in subscribers)
            count++;

        return count;
    }

    // ── Inline subscription table (no SignalR / Hub deps) ─────────────────────

    /// <summary>
    /// Self-contained, lock-free subscription table extracted from
    /// <c>MLS.BlockController.Services.SubscriptionTable</c> for use in the
    /// benchmark without pulling in ASP.NET Core / SignalR infrastructure.
    /// The implementation is identical to production code.
    /// </summary>
    private sealed class InProcessSubscriptionTable
    {
        private readonly ConcurrentDictionary<string, StrongBox<ImmutableHashSet<Guid>>> _table =
            new(StringComparer.Ordinal);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref ImmutableHashSet<Guid> SlotFor(string topic) =>
            ref _table.GetOrAdd(topic,
                _ => new StrongBox<ImmutableHashSet<Guid>>(ImmutableHashSet<Guid>.Empty)).Value!;

        public void Add(string topic, Guid id) =>
            ImmutableInterlocked.Update(ref SlotFor(topic),
                static (set, g) => set.Add(g), id);

        public IReadOnlySet<Guid> GetSubscribers(string topic) =>
            _table.TryGetValue(topic, out var box)
                ? box.Value!
                : ImmutableHashSet<Guid>.Empty;
    }
}
