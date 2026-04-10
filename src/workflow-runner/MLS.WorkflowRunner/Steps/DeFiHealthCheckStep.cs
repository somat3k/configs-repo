namespace MLS.WorkflowRunner.Steps;

/// <summary>
/// Checks DeFi health factors and open positions via the Data Layer.
/// </summary>
public sealed class DeFiHealthCheckStep(HttpClient _http) : IWorkflowStep
{
    public string StepName => "defi-health-check";

    public async Task<IReadOnlyList<StepResult>> ExecuteAsync(
        WorkflowDefinition def, int cycle, IWorkflowLogger logger, CancellationToken ct)
    {
        var results = new List<StepResult>();
        logger.LogStepStart(cycle, def.Cycles, StepName, "defi");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var url  = $"{def.DataLayerUrl}/api/defi/health";
            var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var r = new StepResult(StepName, "defi", "warn", sw.ElapsedMilliseconds, "http-error", $"{(int)resp.StatusCode}");
                results.Add(r);
                logger.LogStepComplete(r, cycle, def.Cycles);
                return results;
            }

            var json   = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc    = JsonDocument.Parse(json);
            var root   = doc.RootElement;
            var hf     = root.TryGetProperty("health_factor",  out var hv) ? hv.GetDouble() : 0.0;
            var positions = root.TryGetProperty("positions",   out var pv) ? pv.GetArrayLength() : 0;
            var protocol  = root.TryGetProperty("protocol",    out var pr) ? pr.GetString() ?? "unknown" : "unknown";

            var status = hf < 1.1 ? "error" : hf < 1.3 ? "warn" : "ok";
            var value  = $"protocol={protocol} health_factor={hf:F3} positions={positions}";
            var result = new StepResult(StepName, "defi", status, sw.ElapsedMilliseconds, value);
            results.Add(result);
            logger.LogStepComplete(result, cycle, def.Cycles);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            var r = new StepResult(StepName, "defi", "warn", sw.ElapsedMilliseconds, "unavailable", ex.Message);
            results.Add(r);
            logger.LogStepComplete(r, cycle, def.Cycles);
        }

        return results;
    }
}
