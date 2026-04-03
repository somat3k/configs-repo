using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLS.BlockController.Services;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;
using Xunit;

namespace MLS.BlockController.Tests.Services;

/// <summary>
/// Verifies <see cref="StrategyRouter"/> correctly populates the subscription table
/// on deploy and clears it on stop.
/// </summary>
public sealed class StrategyRouterTests
{
    private readonly SubscriptionTable _subscriptionTable = new();
    private readonly Mock<IMessageRouter> _routerMock     = new();
    private readonly StrategyRouter _router;

    public StrategyRouterTests()
    {
        _routerMock
            .Setup(r => r.BroadcastAsync(It.IsAny<EnvelopePayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _router = new StrategyRouter(
            _subscriptionTable,
            _routerMock.Object,
            NullLogger<StrategyRouter>.Instance);
    }

    // ── DeployAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeployAsync_RoutesEnvelopesToSubscribedModules()
    {
        var strategyId    = Guid.NewGuid();
        var fromBlockId   = Guid.NewGuid();
        var toBlockId     = Guid.NewGuid();

        var graph = BuildGraph(strategyId, fromBlockId, toBlockId, "candle_output");

        await _router.DeployAsync(graph);

        var topic       = StrategyRouter.BuildTopic(strategyId, fromBlockId, "candle_output");
        var subscribers = _subscriptionTable.GetSubscribers(topic);

        subscribers.Should().Contain(toBlockId);
    }

    [Fact]
    public async Task DeployAsync_BroadcastsStrategyStateChange_Running()
    {
        var graph = BuildGraph(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "candle_output");

        await _router.DeployAsync(graph);

        _routerMock.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.StrategyStateChange),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeployAsync_MultipleConnections_AllRegistered()
    {
        var strategyId  = Guid.NewGuid();
        var blockA      = Guid.NewGuid();
        var blockB      = Guid.NewGuid();
        var blockC      = Guid.NewGuid();

        var graph = new StrategyGraphPayload(
            GraphId:       strategyId,
            Name:          "Multi-hop",
            SchemaVersion: 1,
            Blocks:        new[]
            {
                new StrategyBlockSpec(blockA, "CandleFeedBlock", default),
                new StrategyBlockSpec(blockB, "RSIBlock", default),
                new StrategyBlockSpec(blockC, "ModelTInferenceBlock", default),
            },
            Connections:   new[]
            {
                new StrategyConnectionSpec("c1", blockA, "candle_output", blockB, "candle_input"),
                new StrategyConnectionSpec("c2", blockB, "indicator_output", blockC, "feature_input"),
            });

        await _router.DeployAsync(graph);

        var topicAB = StrategyRouter.BuildTopic(strategyId, blockA, "candle_output");
        var topicBC = StrategyRouter.BuildTopic(strategyId, blockB, "indicator_output");

        _subscriptionTable.GetSubscribers(topicAB).Should().Contain(blockB);
        _subscriptionTable.GetSubscribers(topicBC).Should().Contain(blockC);
    }

    // ── StopAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task StopAsync_ClearsStrategySubscriptions()
    {
        var strategyId  = Guid.NewGuid();
        var fromBlockId = Guid.NewGuid();
        var toBlockId   = Guid.NewGuid();

        var graph = BuildGraph(strategyId, fromBlockId, toBlockId, "candle_output");
        await _router.DeployAsync(graph);

        await _router.StopAsync(strategyId);

        var topic       = StrategyRouter.BuildTopic(strategyId, fromBlockId, "candle_output");
        var subscribers = _subscriptionTable.GetSubscribers(topic);

        subscribers.Should().BeEmpty();
    }

    [Fact]
    public async Task StopAsync_BroadcastsStrategyStateChange_Stopped()
    {
        var strategyId = Guid.NewGuid();
        await _router.StopAsync(strategyId);

        _routerMock.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.StrategyStateChange),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeployAsync_CycleInGraph_Throws()
    {
        var strategyId = Guid.NewGuid();
        var blockA     = Guid.NewGuid();
        var blockB     = Guid.NewGuid();

        var cyclicGraph = new StrategyGraphPayload(
            GraphId:       strategyId,
            Name:          "Cyclic",
            SchemaVersion: 1,
            Blocks:        new[]
            {
                new StrategyBlockSpec(blockA, "RSIBlock", default),
                new StrategyBlockSpec(blockB, "MACDBlock", default),
            },
            Connections:   new[]
            {
                new StrategyConnectionSpec("c1", blockA, "indicator_output", blockB, "candle_input"),
                new StrategyConnectionSpec("c2", blockB, "indicator_output", blockA, "candle_input"),
            });

        var act = async () => await _router.DeployAsync(cyclicGraph);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cycle*");
    }

    [Fact]
    public async Task DeployAsync_SelfLoop_Throws()
    {
        var strategyId = Guid.NewGuid();
        var blockA     = Guid.NewGuid();

        var selfLoopGraph = new StrategyGraphPayload(
            GraphId:       strategyId,
            Name:          "Self-loop",
            SchemaVersion: 1,
            Blocks:        new[] { new StrategyBlockSpec(blockA, "RSIBlock", default) },
            Connections:   new[] { new StrategyConnectionSpec("c1", blockA, "indicator_output", blockA, "candle_input") });

        var act = async () => await _router.DeployAsync(selfLoopGraph);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*self-loop*");
    }

    [Fact]
    public async Task DeployAsync_InvalidSchemaVersion_Throws()
    {
        var graph = new StrategyGraphPayload(
            GraphId:       Guid.NewGuid(),
            Name:          "Bad",
            SchemaVersion: 0,
            Blocks:        new[] { new StrategyBlockSpec(Guid.NewGuid(), "RSIBlock", default) },
            Connections:   Array.Empty<StrategyConnectionSpec>());

        var act = async () => await _router.DeployAsync(graph);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*SchemaVersion*");
    }

    // ── BuildTopic ────────────────────────────────────────────────────────────

    [Fact]
    public void BuildTopic_Deterministic_ForSameInputs()
    {
        var strategyId = Guid.NewGuid();
        var blockId    = Guid.NewGuid();

        var t1 = StrategyRouter.BuildTopic(strategyId, blockId, "candle_output");
        var t2 = StrategyRouter.BuildTopic(strategyId, blockId, "candle_output");

        t1.Should().Be(t2);
    }

    // ── BacktestAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BacktestAsync_BroadcastsStrategyStateChange_Backtesting()
    {
        var strategyId = Guid.NewGuid();

        await _router.BacktestAsync(
            strategyId,
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow);

        _routerMock.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.StrategyStateChange),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static StrategyGraphPayload BuildGraph(
        Guid strategyId, Guid fromBlockId, Guid toBlockId, string fromSocket) =>
        new(
            GraphId:       strategyId,
            Name:          "Test Strategy",
            SchemaVersion: 1,
            Blocks:        new[]
            {
                new StrategyBlockSpec(fromBlockId, "CandleFeedBlock", default),
                new StrategyBlockSpec(toBlockId,   "RSIBlock", default),
            },
            Connections:   new[]
            {
                new StrategyConnectionSpec("c1", fromBlockId, fromSocket, toBlockId, "candle_input"),
            });
}
