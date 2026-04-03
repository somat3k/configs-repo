using System.Collections.Concurrent;
using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Arbitrage;

/// <summary>
/// nHOP path finder block — finds profitable multi-hop arbitrage paths across Arbitrum DEXes.
/// Uses BFS (breadth-first search) with up to 4 hops over a directed token-exchange graph.
/// </summary>
/// <remarks>
/// <para>
/// Supported tokens: WETH, USDC, ARB, WBTC, GMX, RDNT. <br/>
/// Supported exchanges: Camelot, DFYN, Balancer, Morpho.
/// </para>
/// <para>
/// Input: <see cref="BlockSocketType.OnChainEvent"/> signals carrying
/// <c>{ exchange, tokenIn, tokenOut, price, fee, gasUsd }</c> edge price updates. <br/>
/// Output: <see cref="BlockSocketType.PathUpdate"/> with top-3 profitable paths.
/// </para>
/// <para>
/// Algorithm: BFS depth ≤ MaxHops from startToken, scored by
/// <c>profit = outputAmount − inputAmount − totalGas</c>.
/// Emits only paths where <c>profit / inputAmount &gt; MinProfitRatio</c>.
/// </para>
/// </remarks>
public sealed class nHOPPathFinderBlock : BlockBase
{
    // Token universe
    private static readonly IReadOnlyList<string> SupportedTokens =
        ["WETH", "USDC", "ARB", "WBTC", "GMX", "RDNT"];

    // ── Graph edge: (tokenIn, tokenOut, exchange) → (price, fee, gasUsd) ────────
    private sealed record GraphEdge(string TokenIn, string TokenOut, string Exchange,
                                    decimal Price, decimal Fee, decimal GasUsd);

    // Primary graph: (tokenIn, tokenOut) → list of edges per exchange
    private readonly Dictionary<(string TokenIn, string TokenOut), List<GraphEdge>> _graph = new();
    private readonly object _graphLock = new();

    private readonly BlockParameter<string> _startTokenParam =
        new("StartToken", "Start Token", "Token to start and end the arbitrage path (e.g. 'WETH')", "WETH");
    private readonly BlockParameter<int> _maxHopsParam =
        new("MaxHops", "Max Hops", "Maximum swap hops in a single path (2–4)", 3,
            MinValue: 2, MaxValue: 4, IsOptimizable: false);
    private readonly BlockParameter<decimal> _inputAmountParam =
        new("InputAmount", "Input Amount (USD)", "Notional input amount in USD for profit calculation", 10_000m,
            MinValue: 100m, MaxValue: 1_000_000m, IsOptimizable: false);
    private readonly BlockParameter<decimal> _minProfitUsdParam =
        new("MinProfitUsd", "Min Profit (USD)", "Minimum net profit in USD after gas to emit a path", 10m,
            MinValue: 0.01m, MaxValue: 10_000m, IsOptimizable: true);

    /// <inheritdoc/>
    public override string BlockType   => "nHOPPathFinderBlock";
    /// <inheritdoc/>
    public override string DisplayName => "nHOP Path Finder";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_startTokenParam, _maxHopsParam, _inputAmountParam, _minProfitUsdParam];

    /// <summary>Initialises a new <see cref="nHOPPathFinderBlock"/>.</summary>
    public nHOPPathFinderBlock() : base(
        [BlockSocket.Input("price_update", BlockSocketType.OnChainEvent)],
        [BlockSocket.Output("path_update", BlockSocketType.PathUpdate)]) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        lock (_graphLock)
            _graph.Clear();
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.OnChainEvent)
            return new ValueTask<BlockSignal?>(result: null);

        // Parse edge update: { exchange, tokenIn, tokenOut, price, fee, gasUsd }
        if (!TryParseEdge(signal.Value, out var edge))
            return new ValueTask<BlockSignal?>(result: null);

        lock (_graphLock)
            UpdateGraph(edge);

        // Run BFS after each graph update
        var paths = FindTopPaths(
            _startTokenParam.DefaultValue,
            _inputAmountParam.DefaultValue,
            _maxHopsParam.DefaultValue,
            _minProfitUsdParam.DefaultValue);

        if (paths.Count == 0)
            return new ValueTask<BlockSignal?>(result: null);

        var output = new
        {
            start_token   = _startTokenParam.DefaultValue,
            input_usd     = _inputAmountParam.DefaultValue,
            paths         = paths.Select(p => new
            {
                hops          = p.Hops,
                output_usd    = p.OutputUsd,
                gas_usd       = p.TotalGasUsd,
                net_profit    = p.NetProfit,
                profit_ratio  = p.ProfitRatio,
            }).ToArray()
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "path_update", BlockSocketType.PathUpdate, output));
    }

    // ── Graph management ─────────────────────────────────────────────────────────

    private void UpdateGraph(GraphEdge edge)
    {
        var key = (edge.TokenIn, edge.TokenOut);
        if (!_graph.TryGetValue(key, out var edges))
        {
            edges = new List<GraphEdge>(4);
            _graph[key] = edges;
        }

        var idx = edges.FindIndex(e => string.Equals(e.Exchange, edge.Exchange, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) edges[idx] = edge;
        else          edges.Add(edge);
    }

    // ── BFS path finding ─────────────────────────────────────────────────────────

    private sealed record ArbPath(
        IReadOnlyList<HopStep> Hops,
        decimal OutputUsd,
        decimal TotalGasUsd,
        decimal NetProfit,
        decimal ProfitRatio);

    private sealed record HopStep(string TokenIn, string TokenOut, string Exchange, decimal Price);

    private List<ArbPath> FindTopPaths(string startToken, decimal inputUsd, int maxHops, decimal minProfitUsd)
    {
        var candidates = new List<ArbPath>();

        // BFS queue: (currentToken, hopsPath, currentAmount, totalGas)
        var queue = new Queue<(string Token, List<HopStep> Hops, decimal Amount, decimal Gas)>();
        queue.Enqueue((startToken, [], inputUsd, 0m));

        List<GraphEdge>? edges;
        while (queue.Count > 0)
        {
            var (token, hops, amount, gas) = queue.Dequeue();

            if (hops.Count > 0 && string.Equals(token, startToken, StringComparison.OrdinalIgnoreCase))
            {
                // Completed a cycle back to start — evaluate profit
                var netProfit   = amount - inputUsd - gas;
                var profitRatio = inputUsd > 0 ? netProfit / inputUsd : 0m;

                if (netProfit >= minProfitUsd)
                {
                    candidates.Add(new ArbPath(
                        Hops:        hops.AsReadOnly(),
                        OutputUsd:   amount,
                        TotalGasUsd: gas,
                        NetProfit:   netProfit,
                        ProfitRatio: profitRatio));
                }
                continue;
            }

            if (hops.Count >= maxHops) continue;

            // Enumerate all edges from current token
            foreach (var kv in _graph)
            {
                if (!string.Equals(kv.Key.TokenIn, token, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var edge in kv.Value)
                {
                    // Avoid re-visiting tokens (except returning to start)
                    if (hops.Count > 0
                        && !string.Equals(edge.TokenOut, startToken, StringComparison.OrdinalIgnoreCase)
                        && hops.Any(h => string.Equals(h.TokenOut, edge.TokenOut, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var nextAmount = amount * (decimal)edge.Price * (1m - edge.Fee);
                    var nextGas    = gas + edge.GasUsd;
                    var nextHops   = new List<HopStep>(hops) { new(token, edge.TokenOut, edge.Exchange, edge.Price) };

                    queue.Enqueue((edge.TokenOut, nextHops, nextAmount, nextGas));
                }
            }
        }

        // Return top 3 paths by profit/capital ratio
        return [.. candidates
            .OrderByDescending(p => p.ProfitRatio)
            .Take(3)];
    }

    // ── Parsing ──────────────────────────────────────────────────────────────────

    private static bool TryParseEdge(JsonElement value, out GraphEdge edge)
    {
        edge = null!;
        if (value.ValueKind != JsonValueKind.Object) return false;

        var exchange = GetString(value, "exchange");
        var tokenIn  = GetString(value, "tokenIn")  ?? GetString(value, "token_in");
        var tokenOut = GetString(value, "tokenOut") ?? GetString(value, "token_out");

        if (string.IsNullOrEmpty(exchange) || string.IsNullOrEmpty(tokenIn) || string.IsNullOrEmpty(tokenOut))
            return false;

        var price  = GetDecimal(value, "price");
        var fee    = GetDecimal(value, "fee")    ?? 0.003m;  // default 0.3%
        var gasUsd = GetDecimal(value, "gasUsd") ?? GetDecimal(value, "gas_usd") ?? 0.5m; // default $0.50

        if (price is null or <= 0) return false;

        edge = new GraphEdge(tokenIn, tokenOut, exchange, price.Value, fee, gasUsd);
        return true;
    }

    private static string? GetString(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var el) ? el.GetString() : null;

    private static decimal? GetDecimal(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var el)) return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d)) return d;
        if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), out var s)) return s;
        return null;
    }
}
