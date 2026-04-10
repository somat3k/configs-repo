namespace MLS.WorkflowRunner.Steps;

/// <summary>
/// Requests model-t inference for each (symbol, timeframe) combo from the ML Runtime module.
/// </summary>
public sealed class ModelEvaluationStep(HttpClient _http) : IWorkflowStep
{
    public string StepName => "model-evaluation";

    public async Task<IReadOnlyList<StepResult>> ExecuteAsync(
        WorkflowDefinition def, int cycle, IWorkflowLogger logger, CancellationToken ct)
    {
        var results = new List<StepResult>();

        foreach (var symbol in def.Symbols)
        {
            foreach (var tf in def.Timeframes)
            {
                var label = $"{symbol}@{tf}";
                logger.LogStepStart(cycle, def.Cycles, StepName, label);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var body = JsonSerializer.Serialize(new
                    {
                        model_type = "model-t",
                        symbol,
                        timeframe  = tf,
                    });
                    using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                    var url  = $"{def.DataLayerUrl}/api/inference/run";
                    var resp = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
                    sw.Stop();

                    if (!resp.IsSuccessStatusCode)
                    {
                        var r = new StepResult(StepName, label, "warn", sw.ElapsedMilliseconds, "http-error", $"{(int)resp.StatusCode}");
                        results.Add(r);
                        logger.LogStepComplete(r, cycle, def.Cycles);
                        continue;
                    }

                    var json  = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var doc   = JsonDocument.Parse(json);
                    var root  = doc.RootElement;
                    var signal     = root.TryGetProperty("signal",     out var sv) ? sv.GetString() ?? "HOLD" : "HOLD";
                    var confidence = root.TryGetProperty("confidence", out var cv) ? cv.GetDouble() : 0.0;

                    var value  = $"signal={signal} confidence={confidence:F3}";
                    var result = new StepResult(StepName, label, "ok", sw.ElapsedMilliseconds, value);
                    results.Add(result);
                    logger.LogStepComplete(result, cycle, def.Cycles);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    sw.Stop();
                    var r = new StepResult(StepName, label, "warn", sw.ElapsedMilliseconds, "unavailable", ex.Message);
                    results.Add(r);
                    logger.LogStepComplete(r, cycle, def.Cycles);
                }
            }
        }

        return results;
    }
}
