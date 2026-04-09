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
/// Hot path (<see cref="PublishPrice"/>) is lock-free via <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// and writes to a bounded <see cref="Channel{T}"/> with <c>DropOldest</c> overflow.
/// </para>
/// </remarks>
public sealed class OpportunityScanner : IOpportunityScanner
{
    // ── Supported token universe ──────────────────────────────────────────────
    private static readonly IReadOnlyList<string> Tokens =
        ["WETH", "USDC", "ARB", "WBTC", "GMX", "RDNT"];

    // ── Price graph: "exchange/tokenIn/tokenOut" → snapshot ──────────────────
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
    public void PublishPrice(PriceSnapshot snapshot)
    {
        var key = PriceKey(snapshot.Exchange, snapshot.Symbol);
        _prices[key] = snapshot;

        // Decompose the symbol into tokenIn/tokenOut and update forward graph
        var parts    = snapshot.Symbol.Split('/');
        if (parts.Length != 2) return;

        var tokenIn  = parts[0].ToUpperInvariant();
        var tokenOut = parts[1].ToUpperInvariant();

        if (!Tokens.Contains(tokenIn, StringComparer.OrdinalIgnoreCase) ||
            !Tokens.Contains(tokenOut, StringComparer.OrdinalIgnoreCase))
            return;

        // After each price update: run BFS and emit opportunities
        var opts = _options.Value;
        var paths = FindPaths(tokenIn, opts.SimulatedInputAmountUsd, opts.MinProfitUsd);

        foreach (var path in paths)
        {
            if (!_channel.Writer.TryWrite(path))
                _logger.LogTrace("Opportunity channel full — oldest item dropped.");
        }
    }

    // ── BFS path finding ─────────────────────────────────────────────────────

    private sealed record EdgeInfo(string Exchange, decimal Price, decimal Fee, decimal GasUsd);

    private List<ArbitrageOpportunity> FindPaths(
        string startToken, decimal inputAmount, decimal minProfitUsd)
    {
        // Build adjacency list from current price graph
        // key: (tokenIn, tokenOut) → list of exchange edges
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
            foreach (var kv in adjacency)
            {
                if (!string.Equals(kv.Key.TokenIn, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var edge in kv.Value)
                {
                    // Avoid re-visiting tokens (except cycling back to start)
                    if (hops.Count > 0 &&
                        !string.Equals(edge.Exchange + kv.Key.TokenOut, startToken, StringComparison.OrdinalIgnoreCase) &&
                        hops.Any(h => string.Equals(h.ToToken, kv.Key.TokenOut, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var nextAmount = amount * edge.Price * (1m - edge.Fee);
                    var nextGas    = gas + edge.GasUsd;
                    var nextHops   = new List<ArbHopDetail>(hops)
                    {
                        new(token, kv.Key.TokenOut, edge.Exchange, edge.Price, edge.Fee, edge.GasUsd)
                    };

                    queue.Enqueue((kv.Key.TokenOut, nextHops, nextAmount, nextGas));
                }
            }
        }

        // Return top 3 by profit ratio
        results.Sort(static (a, b) => b.ProfitRatio.CompareTo(a.ProfitRatio));
        return results.Count > 3 ? results[..3] : results;
    }

    private Dictionary<(string TokenIn, string TokenOut), List<EdgeInfo>> BuildAdjacency()
    {
        var adj = new Dictionary<(string, string), List<EdgeInfo>>(32);

        foreach (var snapshot in _prices.Values)
        {
            var parts = snapshot.Symbol.Split('/');
            if (parts.Length != 2) continue;

            var tIn  = parts[0].ToUpperInvariant();
            var tOut = parts[1].ToUpperInvariant();

            // Estimate fee per exchange (standard LP fees)
            var fee = snapshot.Exchange.ToLowerInvariant() switch
            {
                "camelot"     => 0.003m,
                "dfyn"        => 0.003m,
                "balancer"    => 0.002m,
                "hyperliquid" => 0.001m,
                _             => 0.003m,
            };

            // Estimate gas cost per hop (Arbitrum is cheap; ~0.01-0.05 USD per swap)
            const decimal gasPerHopUsd = 0.03m;

            var key  = (tIn, tOut);
            if (!adj.TryGetValue(key, out var edges))
            {
                edges      = new List<EdgeInfo>(4);
                adj[key]   = edges;
            }

            var idx = edges.FindIndex(e =>
                string.Equals(e.Exchange, snapshot.Exchange, StringComparison.OrdinalIgnoreCase));
            var edge = new EdgeInfo(snapshot.Exchange, snapshot.MidPrice, fee, gasPerHopUsd);
            if (idx >= 0) edges[idx] = edge;
            else          edges.Add(edge);

            // Also add the reverse direction (toToken → fromToken) using 1/price
            if (snapshot.MidPrice > 0)
            {
                var revKey = (tOut, tIn);
                if (!adj.TryGetValue(revKey, out var revEdges))
                {
                    revEdges      = new List<EdgeInfo>(4);
                    adj[revKey]   = revEdges;
                }

                var revEdge = new EdgeInfo(snapshot.Exchange, 1m / snapshot.MidPrice, fee, gasPerHopUsd);
                var revIdx  = revEdges.FindIndex(e =>
                    string.Equals(e.Exchange, snapshot.Exchange, StringComparison.OrdinalIgnoreCase));
                if (revIdx >= 0) revEdges[revIdx] = revEdge;
                else             revEdges.Add(revEdge);
            }
        }

        return adj;
    }

    private static string PriceKey(string exchange, string symbol) =>
        $"{exchange.ToLowerInvariant()}/{symbol.ToUpperInvariant()}";
}
