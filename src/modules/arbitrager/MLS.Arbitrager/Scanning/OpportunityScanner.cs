using System.Collections.Concurrent;
using System.Threading.Channels;
using MLS.Arbitrager.Configuration;

namespace MLS.Arbitrager.Scanning;

/// <summary>
/// nHOP opportunity scanner — maintains a directed token-exchange price graph and runs
/// BFS (breadth-first search) to find circular arbitrage paths with positive net profit.
/// </summary>
/// <remarks>
/// <para>
/// Supported tokens: WETH, USDC, ARB, WBTC, GMX, RDNT.<br/>
/// Supported exchanges: Camelot, DFYN, Balancer, Hyperliquid.
/// </para>
/// <para>
/// <see cref="PublishPrice"/> is allocation-free on the hot path — it updates
/// <see cref="_prices"/> (lock-free <see cref="ConcurrentDictionary{TKey,TValue}"/>)
/// and returns immediately. BFS runs on a dedicated periodic background worker
/// (<c>ScannerWorker</c>) so the price-feed path is never blocked.
/// </para>
/// </remarks>
public sealed class OpportunityScanner : IOpportunityScanner
{
    // ── Supported token universe ──────────────────────────────────────────────
    private static readonly IReadOnlyList<string> Tokens =
        ["WETH", "USDC", "ARB", "WBTC", "GMX", "RDNT"];

    // ── Price graph: "exchange/symbol" → snapshot ─────────────────────────────
    private readonly ConcurrentDictionary<string, PriceSnapshot> _prices = new();

    // ── Output channel ────────────────────────────────────────────────────────
    private readonly Channel<ArbitrageOpportunity> _channel;

    private readonly IOptions<ArbitragerOptions> _options;
    private readonly ILogger<OpportunityScanner> _logger;

    /// <summary>Initialises a new <see cref="OpportunityScanner"/>.</summary>
    public OpportunityScanner(
        IOptions<ArbitragerOptions> options,
        ILogger<OpportunityScanner> logger)
    {
        _options = options;
        _logger  = logger;

        _channel = Channel.CreateBounded<ArbitrageOpportunity>(
            new BoundedChannelOptions(options.Value.OpportunityQueueCapacity)
            {
                FullMode      = BoundedChannelFullMode.DropOldest,
                SingleReader  = false,
                SingleWriter  = false,
            });
    }

    /// <inheritdoc/>
    public ChannelReader<ArbitrageOpportunity> Opportunities => _channel.Reader;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, PriceSnapshot> GetCurrentPrices() => _prices;

    /// <inheritdoc/>
    /// <remarks>
    /// Hot path: updates the price dictionary only.
    /// BFS is triggered by the <c>ScannerWorker</c> background service on a fixed interval.
    /// </remarks>
    public void PublishPrice(PriceSnapshot snapshot)
    {
        var key = PriceKey(snapshot.Exchange, snapshot.Symbol);
        _prices[key] = snapshot;
    }

    /// <summary>
    /// Runs one full BFS scan over all supported start tokens and emits profitable opportunities.
    /// Called by <c>ScannerWorker</c> on a periodic timer — not on the price-feed hot path.
    /// </summary>
    internal void RunScan()
    {
        var opts = _options.Value;

        foreach (var startToken in Tokens)
        {
            var paths = FindPaths(startToken, opts.SimulatedInputAmountUsd, opts.MinProfitUsd);
            foreach (var path in paths)
            {
                if (!_channel.Writer.TryWrite(path))
                    _logger.LogTrace("Opportunity channel full — oldest item dropped.");
            }
        }
    }

    // ── BFS path finding ─────────────────────────────────────────────────────

    private sealed record EdgeInfo(string Exchange, decimal Price, decimal Fee, decimal GasUsd);

    private List<ArbitrageOpportunity> FindPaths(
        string startToken, decimal inputAmount, decimal minProfitUsd)
    {
        // Build adjacency list from current price snapshot (snapshot of concurrent dict)
        var adjacency = BuildAdjacency();

        var results = new List<ArbitrageOpportunity>();

        // BFS queue: (currentToken, hops, currentAmount, totalGas)
        var queue = new Queue<(string Token, List<ArbHopDetail> Hops, decimal Amount, decimal Gas)>();
        queue.Enqueue((startToken, [], inputAmount, 0m));

        while (queue.Count > 0)
        {
            var (token, hops, amount, gas) = queue.Dequeue();

            if (hops.Count > 0 &&
                string.Equals(token, startToken, StringComparison.OrdinalIgnoreCase))
            {
                // Completed a profitable cycle
                var netProfit = amount - inputAmount - gas;
                if (netProfit >= minProfitUsd)
                {
                    var profitRatio = inputAmount > 0 ? netProfit / inputAmount : 0m;
                    var now         = DateTimeOffset.UtcNow;
                    results.Add(new ArbitrageOpportunity(
                        OpportunityId:       Guid.NewGuid(),
                        Hops:                hops.AsReadOnly(),
                        InputAmountUsd:      inputAmount,
                        EstimatedOutputUsd:  amount,
                        GasEstimateUsd:      gas,
                        NetProfitUsd:        netProfit,
                        ProfitRatio:         profitRatio,
                        DetectedAt:          now,
                        ExpiresAt:           now.AddSeconds(2)));
                }
                continue;
            }

            if (hops.Count >= 4) continue; // max 4 hops

            var key = token.ToUpperInvariant();
            if (!adjacency.TryGetValue(key, out var outgoingEdges)) continue;

            foreach (var (toToken, edgeList) in outgoingEdges)
            {
                foreach (var edge in edgeList)
                {
                    // Avoid re-visiting tokens (except cycling back to start)
                    if (hops.Count > 0 &&
                        !string.Equals(toToken, startToken, StringComparison.OrdinalIgnoreCase) &&
                        hops.Any(h => string.Equals(h.ToToken, toToken, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var nextAmount = amount * edge.Price * (1m - edge.Fee);
                    var nextGas    = gas + edge.GasUsd;
                    var nextHops   = new List<ArbHopDetail>(hops)
                    {
                        new(token, toToken, edge.Exchange, edge.Price, edge.Fee, edge.GasUsd)
                    };

                    queue.Enqueue((toToken, nextHops, nextAmount, nextGas));
                }
            }
        }

        // Return top 3 by profit ratio
        results.Sort(static (a, b) => b.ProfitRatio.CompareTo(a.ProfitRatio));
        return results.Count > 3 ? results[..3] : results;
    }

    // ── Adjacency: tokenIn → { tokenOut → [EdgeInfo] } ───────────────────────
    // O(outgoing edges) lookup instead of full dict iteration per dequeue step.

    private Dictionary<string, Dictionary<string, List<EdgeInfo>>> BuildAdjacency()
    {
        var adj = new Dictionary<string, Dictionary<string, List<EdgeInfo>>>(
            Tokens.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in _prices.Values)
        {
            var parts = snapshot.Symbol.Split('/');
            if (parts.Length != 2) continue;

            var tIn  = parts[0].ToUpperInvariant();
            var tOut = parts[1].ToUpperInvariant();

            if (!Tokens.Contains(tIn,  StringComparer.OrdinalIgnoreCase) ||
                !Tokens.Contains(tOut, StringComparer.OrdinalIgnoreCase))
                continue;

            var fee = snapshot.Exchange.ToLowerInvariant() switch
            {
                "camelot"     => 0.003m,
                "dfyn"        => 0.003m,
                "balancer"    => 0.002m,
                "hyperliquid" => 0.001m,
                _             => 0.003m,
            };

            const decimal gasPerHopUsd = 0.03m;

            AddEdge(adj, tIn, tOut, new EdgeInfo(snapshot.Exchange, snapshot.MidPrice, fee, gasPerHopUsd));

            // Reverse direction using 1/price
            if (snapshot.MidPrice > 0)
                AddEdge(adj, tOut, tIn, new EdgeInfo(snapshot.Exchange, 1m / snapshot.MidPrice, fee, gasPerHopUsd));
        }

        return adj;
    }

    private static void AddEdge(
        Dictionary<string, Dictionary<string, List<EdgeInfo>>> adj,
        string from, string to, EdgeInfo edge)
    {
        if (!adj.TryGetValue(from, out var outgoing))
        {
            outgoing  = new Dictionary<string, List<EdgeInfo>>(StringComparer.OrdinalIgnoreCase);
            adj[from] = outgoing;
        }

        if (!outgoing.TryGetValue(to, out var edgeList))
        {
            edgeList    = new List<EdgeInfo>(4);
            outgoing[to] = edgeList;
        }

        var idx = edgeList.FindIndex(e =>
            string.Equals(e.Exchange, edge.Exchange, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) edgeList[idx] = edge;
        else          edgeList.Add(edge);
    }

    private static string PriceKey(string exchange, string symbol) =>
        $"{exchange.ToLowerInvariant()}/{symbol.ToUpperInvariant()}";
}
