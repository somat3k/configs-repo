using Microsoft.Extensions.Logging;
using MLS.BlockController.Constants;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using MLS.Core.Contracts.Designer;

namespace MLS.BlockController.Services;

/// <inheritdoc cref="IStrategyRouter"/>
public sealed class StrategyRouter(
    ISubscriptionTable _subscriptions,
    IMessageRouter _router,
    ILogger<StrategyRouter> _logger) : IStrategyRouter
{
    private const string ModuleId = "block-controller";

    /// <inheritdoc/>
    public async Task DeployAsync(StrategyGraphPayload graph, CancellationToken ct = default)
    {
        _logger.LogInformation("Deploying strategy {GraphId} ({Name}) schema_version={Version}",
            graph.GraphId, graph.Name, graph.SchemaVersion);

        ValidateGraph(graph);

        // 1. Clear any existing subscriptions for this strategy
        await _subscriptions.ClearStrategyAsync(graph.GraphId, ct).ConfigureAwait(false);

        // 2. For every connection, register: topic = "{strategyId}/{fromBlockId}/{fromSocket}"
        //    → subscriber = the module that owns the destination block
        //    In Session 02 we register the block IDs directly; module mapping is resolved
        //    in a later session when modules declare their block ownership.
        foreach (var connection in graph.Connections)
        {
            var topic = BuildTopic(graph.GraphId, connection.FromBlockId, connection.FromSocket);
            // For now, subscribe the destination block ID as a placeholder module reference.
            // The full module-to-block binding is wired in Session 04 (Designer module).
            await _subscriptions.AddAsync(topic, connection.ToBlockId, ct).ConfigureAwait(false);

            _logger.LogDebug("Subscribed block {ToBlock} to topic {Topic}",
                connection.ToBlockId, topic);
        }

        // 3. Broadcast STRATEGY_STATE_CHANGE(Running)
        var stateChange = new StrategyStateChangePayload(
            StrategyId: graph.GraphId,
            PreviousState: StrategyState.Stopped,
            CurrentState: StrategyState.Running,
            Timestamp: DateTimeOffset.UtcNow);

        var envelope = EnvelopePayload.Create(
            MessageTypes.StrategyStateChange, ModuleId, stateChange);

        await _router.BroadcastAsync(envelope, ct).ConfigureAwait(false);

        _logger.LogInformation("Strategy {GraphId} deployed and running", graph.GraphId);
    }

    /// <inheritdoc/>
    public async Task StopAsync(Guid strategyId, CancellationToken ct = default)
    {
        _logger.LogInformation("Stopping strategy {StrategyId}", strategyId);

        await _subscriptions.ClearStrategyAsync(strategyId, ct).ConfigureAwait(false);

        var stateChange = new StrategyStateChangePayload(
            StrategyId: strategyId,
            PreviousState: StrategyState.Running,
            CurrentState: StrategyState.Stopped,
            Timestamp: DateTimeOffset.UtcNow);

        var envelope = EnvelopePayload.Create(
            MessageTypes.StrategyStateChange, ModuleId, stateChange);

        await _router.BroadcastAsync(envelope, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task BacktestAsync(Guid strategyId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting backtest for strategy {StrategyId} from {From} to {To}",
            strategyId, from, to);

        var stateChange = new StrategyStateChangePayload(
            StrategyId: strategyId,
            PreviousState: StrategyState.Stopped,
            CurrentState: StrategyState.Backtesting,
            Timestamp: DateTimeOffset.UtcNow);

        var envelope = EnvelopePayload.Create(
            MessageTypes.StrategyStateChange, ModuleId, stateChange);

        await _router.BroadcastAsync(envelope, ct).ConfigureAwait(false);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Build a deterministic, readable topic key for a block output socket.</summary>
    public static string BuildTopic(Guid strategyId, Guid blockId, string socketName) =>
        $"{strategyId:N}/{blockId:N}/{socketName}";

    private static void ValidateGraph(StrategyGraphPayload graph)
    {
        if (graph.SchemaVersion < 1)
        {
            throw new ArgumentException($"Graph {graph.GraphId}: SchemaVersion must be >= 1.", nameof(graph));
        }

        if (graph.Blocks.Count == 0)
        {
            throw new ArgumentException($"Graph {graph.GraphId}: must contain at least one block.", nameof(graph));
        }

        // Detect direct self-loops (block → itself on same socket)
        foreach (var conn in graph.Connections)
        {
            if (conn.FromBlockId == conn.ToBlockId)
            {
                throw new ArgumentException(
                    $"Graph {graph.GraphId}: self-loop detected on block {conn.FromBlockId}.", nameof(graph));
            }
        }

        // Detect cycles using DFS over the connection graph
        var adjacency = graph.Connections
            .GroupBy(c => c.FromBlockId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.ToBlockId).ToHashSet());

        var blockIds = graph.Blocks.Select(b => b.BlockId).ToHashSet();
        var visited = new HashSet<Guid>(blockIds.Count);
        var inStack = new HashSet<Guid>(blockIds.Count);

        foreach (var blockId in blockIds)
        {
            if (!visited.Contains(blockId))
            {
                DetectCycle(blockId, adjacency, visited, inStack, graph.GraphId);
            }
        }
    }

    private static void DetectCycle(
        Guid node,
        Dictionary<Guid, HashSet<Guid>> adjacency,
        HashSet<Guid> visited,
        HashSet<Guid> inStack,
        Guid graphId)
    {
        visited.Add(node);
        inStack.Add(node);

        if (adjacency.TryGetValue(node, out var neighbours))
        {
            foreach (var neighbour in neighbours)
            {
                if (inStack.Contains(neighbour))
                {
                    throw new ArgumentException(
                        $"Graph {graphId}: cycle detected involving block {neighbour}.",
                        "graph");
                }

                if (!visited.Contains(neighbour))
                {
                    DetectCycle(neighbour, adjacency, visited, inStack, graphId);
                }
            }
        }

        inStack.Remove(node);
    }
}
