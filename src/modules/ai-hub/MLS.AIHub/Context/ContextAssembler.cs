using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MLS.AIHub.Configuration;

namespace MLS.AIHub.Context;

/// <summary>
/// Contract for assembling a live <see cref="ProjectSnapshot"/> from all MLS modules.
/// </summary>
public interface IContextAssembler
{
    /// <summary>
    /// Assembles a snapshot by querying all module sources in parallel.
    /// Target completion time: &lt;200ms. Never throws; failed sources are recorded
    /// in <see cref="ProjectSnapshot.FailedSources"/>.
    /// </summary>
    Task<ProjectSnapshot> AssembleAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Assembles a live <see cref="ProjectSnapshot"/> from all MLS modules in parallel,
/// completing within the configured timeout (default 200ms).
/// </summary>
/// <remarks>
/// Data sources queried (8 total):
/// <list type="number">
///   <item>Block Controller — registered modules + health</item>
///   <item>Trader — open positions</item>
///   <item>Trader — recent ML signals</item>
///   <item>Arbitrager — active opportunities</item>
///   <item>DeFi — position health factors</item>
///   <item>ML Runtime — registered models + metrics</item>
///   <item>Designer — active strategies</item>
///   <item>Block Controller — recent envelope history</item>
/// </list>
/// Each source has an individual per-source timeout so a slow module
/// never blocks the rest of the snapshot.
/// </remarks>
public sealed class ContextAssembler(
    IHttpClientFactory _httpFactory,
    IOptions<AIHubOptions> _options,
    ILogger<ContextAssembler> _logger) : IContextAssembler
{
    private static readonly TimeSpan PerSourceTimeout = TimeSpan.FromMilliseconds(120);

    /// <inheritdoc/>
    public async Task<ProjectSnapshot> AssembleAsync(Guid userId, CancellationToken ct = default)
    {
        var opts      = _options.Value;
        var sw        = Stopwatch.StartNew();
        var failed    = new List<string>();

        // Launch all 8 sources in parallel
        var modulesTask     = FetchJsonAsync<ModuleInfo>    (opts.BlockControllerUrl, "/api/modules",                        "block-controller/modules",  failed, ct);
        var positionsTask   = FetchJsonAsync<PositionInfo>  (opts.TraderUrl,          "/api/positions",                       "trader/positions",          failed, ct);
        var signalsTask     = FetchJsonAsync<SignalInfo>     (opts.TraderUrl,          $"/api/signals/recent?n={opts.MaxSignalHistory}", "trader/signals", failed, ct);
        var arbTask         = FetchRawArrayAsync             (opts.ArbitragerUrl,      "/api/opportunities/active",            "arbitrager/opportunities",  failed, ct);
        var defiTask        = FetchJsonAsync<DefiHealthInfo> (opts.DeFiUrl,            "/api/positions/health",                "defi/health",               failed, ct);
        var modelsTask      = FetchJsonAsync<ModelInfo>      (opts.MlRuntimeUrl,       "/api/models",                         "ml-runtime/models",         failed, ct);
        var strategiesTask  = FetchJsonAsync<StrategyInfo>   (opts.DesignerUrl,        "/api/strategies/active",               "designer/strategies",       failed, ct);
        var envelopesTask   = FetchRawArrayAsync             (opts.BlockControllerUrl, $"/api/envelopes/recent?n={opts.MaxEnvelopeHistory}", "block-controller/envelopes", failed, ct);

        await Task.WhenAll(
            modulesTask, positionsTask, signalsTask, arbTask, defiTask,
            modelsTask, strategiesTask, envelopesTask)
            .ConfigureAwait(false);

        // Await each completed task individually — avoids .Result which can surface AggregateException
        var modules     = await modulesTask.ConfigureAwait(false);
        var positions   = await positionsTask.ConfigureAwait(false);
        var signals     = await signalsTask.ConfigureAwait(false);
        var arb         = await arbTask.ConfigureAwait(false);
        var defi        = await defiTask.ConfigureAwait(false);
        var models      = await modelsTask.ConfigureAwait(false);
        var strategies  = await strategiesTask.ConfigureAwait(false);
        var envelopes   = await envelopesTask.ConfigureAwait(false);

        sw.Stop();

        if (sw.ElapsedMilliseconds > opts.ContextAssemblyTimeoutMs)
        {
            _logger.LogWarning(
                "ContextAssembler exceeded target {Target}ms — actual {Actual}ms. Failed sources: [{Sources}]",
                opts.ContextAssemblyTimeoutMs,
                sw.ElapsedMilliseconds,
                string.Join(", ", failed));
        }

        return new ProjectSnapshot
        {
            AssembledAt      = DateTimeOffset.UtcNow,
            AssemblyMs       = sw.ElapsedMilliseconds,
            Modules          = modules     ?? [],
            OpenPositions    = positions   ?? [],
            RecentSignals    = signals     ?? [],
            ArbOpportunities = arb,
            DefiHealth       = defi        ?? [],
            MlModels         = models      ?? [],
            ActiveStrategies = strategies  ?? [],
            EnvelopeHistory  = envelopes,
            FailedSources    = failed.AsReadOnly(),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches and deserialises a JSON array from <paramref name="baseUrl"/>/<paramref name="path"/>.
    /// Returns <see langword="null"/> (and records the source in <paramref name="failed"/>)
    /// on timeout or any HTTP error.
    /// </summary>
    private async Task<List<T>?> FetchJsonAsync<T>(
        string baseUrl, string path, string sourceName,
        List<string> failed, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(PerSourceTimeout);

        try
        {
            using var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl);

            return await client.GetFromJsonAsync<List<T>>(path, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // propagate caller cancellation
        }
        catch (Exception ex)
        {
            lock (failed) failed.Add(sourceName);
            _logger.LogDebug(ex, "ContextAssembler: source {Source} failed", sourceName);
            return null;
        }
    }

    /// <summary>
    /// Fetches a JSON array endpoint and returns a list of raw <see cref="JsonElement"/> items.
    /// Returns an empty list on timeout or error.
    /// </summary>
    private async Task<IReadOnlyList<JsonElement>> FetchRawArrayAsync(
        string baseUrl, string path, string sourceName,
        List<string> failed, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(PerSourceTimeout);

        try
        {
            using var client = _httpFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl);

            var elements = await client.GetFromJsonAsync<List<JsonElement>>(path, cts.Token)
                                       .ConfigureAwait(false);
            return (IReadOnlyList<JsonElement>?)elements ?? [];
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            lock (failed) failed.Add(sourceName);
            _logger.LogDebug(ex, "ContextAssembler: source {Source} failed", sourceName);
            return [];
        }
    }
}
