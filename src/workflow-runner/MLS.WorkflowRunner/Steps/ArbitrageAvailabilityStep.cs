namespace MLS.WorkflowRunner.Steps;

/// <summary>
/// Scans for arbitrage opportunities across configured symbols using the Data Layer nHOP endpoint.
/// </summary>
public sealed class ArbitrageAvailabilityStep(HttpClient _http) : IWorkflowStep
{
    public string StepName => "arbitrage-availability";

    public async Task<IReadOnlyList<StepResult>> ExecuteAsync(
        WorkflowDefinition def, int cycle, IWorkflowLogger logger, CancellationToken ct)
    {
        var results  = new List<StepResult>();
        var symbolsCsv = string.Join(",", def.Symbols);
        var symbol   = string.Join("+", def.Symbols);

        logger.LogStepStart(cycle, def.Cycles, StepName, symbol);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var url  = $"{def.DataLayerUrl}/api/arbitrage/opportunities?symbols={Uri.EscapeDataString(symbolsCsv)}";
            var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var r = new StepResult(StepName, symbol, "warn", sw.ElapsedMilliseconds, "http-error", $"{(int)resp.StatusCode}");
                results.Add(r);
                logger.LogStepComplete(r, cycle, def.Cycles);
                return results;
            }

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var paths    = root.TryGetProperty("paths",  out var pv) ? pv.GetArrayLength() : 0;
            var maxSpread = root.TryGetProperty("max_spread_pct", out var sv) ? sv.GetDouble() : 0.0;

            var value  = $"paths={paths} max_spread={maxSpread:F4}%";
            var status = paths > 0 ? "ok" : "ok";
            var result = new StepResult(StepName, symbol, status, sw.ElapsedMilliseconds, value);
            results.Add(result);
            logger.LogStepComplete(result, cycle, def.Cycles);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            var r = new StepResult(StepName, symbol, "warn", sw.ElapsedMilliseconds, "unavailable", ex.Message);
            results.Add(r);
            logger.LogStepComplete(r, cycle, def.Cycles);
        }

        return results;
    }
}
