using FluentAssertions;
using MLS.BlockController.Services;
using Xunit;

namespace MLS.BlockController.Tests.Services;

/// <summary>
/// Verifies <see cref="SubscriptionTable"/> thread-safety, O(1) lookup correctness,
/// and strategy-scoped clear behaviour.
/// </summary>
public sealed class SubscriptionTableTests
{
    private readonly SubscriptionTable _table = new();

    // ── Add / GetSubscribers ───────────────────────────────────────────────────

    [Fact]
    public async Task Add_ThenGet_ReturnsSubscriber()
    {
        var topic    = "BLOCK_SIGNAL";
        var moduleId = Guid.NewGuid();

        await _table.AddAsync(topic, moduleId);

        _table.GetSubscribers(topic).Should().Contain(moduleId);
    }

    [Fact]
    public async Task Add_MultipleModules_AllReturned()
    {
        var topic = "TRADE_SIGNAL";
        var m1    = Guid.NewGuid();
        var m2    = Guid.NewGuid();

        await _table.AddAsync(topic, m1);
        await _table.AddAsync(topic, m2);

        _table.GetSubscribers(topic).Should().BeEquivalentTo(new[] { m1, m2 });
    }

    [Fact]
    public async Task Add_SameModuleTwice_NoDuplicates()
    {
        var topic    = "BLOCK_SIGNAL";
        var moduleId = Guid.NewGuid();

        await _table.AddAsync(topic, moduleId);
        await _table.AddAsync(topic, moduleId);

        _table.GetSubscribers(topic).Should().HaveCount(1);
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_ExistingSubscriber_RemovesIt()
    {
        var topic    = "TRADE_SIGNAL";
        var moduleId = Guid.NewGuid();

        await _table.AddAsync(topic, moduleId);
        await _table.RemoveAsync(topic, moduleId);

        _table.GetSubscribers(topic).Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_NonExistentSubscriber_NoThrow()
    {
        var act = async () => await _table.RemoveAsync("NO_TOPIC", Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    // ── RemoveAll ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAll_RemovesFromAllTopics()
    {
        var moduleId = Guid.NewGuid();
        await _table.AddAsync("TOPIC_A", moduleId);
        await _table.AddAsync("TOPIC_B", moduleId);

        await _table.RemoveAllAsync(moduleId);

        _table.GetSubscribers("TOPIC_A").Should().BeEmpty();
        _table.GetSubscribers("TOPIC_B").Should().BeEmpty();
    }

    // ── GetSubscribers on missing topic ───────────────────────────────────────

    [Fact]
    public void GetSubscribers_MissingTopic_ReturnsEmpty()
    {
        _table.GetSubscribers("DOES_NOT_EXIST").Should().BeEmpty();
    }

    // ── ClearStrategy ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearStrategy_RemovesOnlyStrategyTopics()
    {
        var strategyId = Guid.NewGuid();
        var otherId    = Guid.NewGuid();
        var moduleId   = Guid.NewGuid();

        var strategyTopic = StrategyRouter.BuildTopic(strategyId, Guid.NewGuid(), "candle_output");
        var otherTopic    = StrategyRouter.BuildTopic(otherId,    Guid.NewGuid(), "candle_output");

        await _table.AddAsync(strategyTopic, moduleId);
        await _table.AddAsync(otherTopic,    moduleId);

        await _table.ClearStrategyAsync(strategyId);

        _table.GetSubscribers(strategyTopic).Should().BeEmpty("strategy topics cleared");
        _table.GetSubscribers(otherTopic).Should().Contain(moduleId, "other strategy untouched");
    }

    // ── GetSnapshot ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSnapshot_ReflectsCurrentState()
    {
        var topic    = "SNAP_TEST";
        var moduleId = Guid.NewGuid();

        await _table.AddAsync(topic, moduleId);

        var snapshot = _table.GetSnapshot();

        snapshot.Should().ContainKey(topic);
        snapshot[topic].Should().Contain(moduleId);
    }

    // ── Concurrent add/remove ─────────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentAdds_NoDuplicatesOrExceptions()
    {
        var topic   = "CONCURRENT";
        var modules = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();

        await Task.WhenAll(modules.Select(m => _table.AddAsync(topic, m).AsTask()));

        _table.GetSubscribers(topic).Should().HaveCount(100);
    }
}
