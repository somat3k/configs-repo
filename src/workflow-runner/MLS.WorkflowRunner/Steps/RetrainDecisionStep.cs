namespace MLS.WorkflowRunner.Steps;

/// <summary>
/// Evaluates whether a model retrain should be triggered based on the
/// <see cref="RetrainGateOptions"/> thresholds. Fires TRAINING_JOB_START if needed.
/// </summary>
public sealed class RetrainDecisionStep(HttpClient _http) : IWorkflowStep
{
    public string StepName => "retrain-decision";

    public async Task<IReadOnlyList<StepResult>> ExecuteAsync(
        WorkflowDefinition def, int cycle, IWorkflowLogger logger, CancellationToken ct)
    {
        var results = new List<StepResult>();
        var gate    = def.RetrainGate;

        foreach (var symbol in def.Symbols)
        {
            logger.LogStepStart(cycle, def.Cycles, StepName, symbol);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Query the latest evaluation metrics from Data Layer
                var metricsUrl = $"{def.DataLayerUrl}/api/metrics/latest?symbol={symbol}&model=model-t";
                var resp = await _http.GetAsync(metricsUrl, ct).ConfigureAwait(false);
                sw.Stop();

                bool triggerRetrain;
                string reason;

                if (!resp.IsSuccessStatusCode)
                {
                    // Can't get metrics → skip retrain decision
                    var r = new StepResult(StepName, symbol, "warn", sw.ElapsedMilliseconds, "metrics-unavailable", $"{(int)resp.StatusCode}");
                    results.Add(r);
                    logger.LogStepComplete(r, cycle, def.Cycles);
                    continue;
                }

                var json   = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var doc    = JsonDocument.Parse(json);
                var root   = doc.RootElement;
                var acc    = root.TryGetProperty("accuracy", out var av) ? av.GetDouble() : 1.0;
                var f1     = root.TryGetProperty("f1",       out var fv) ? fv.GetDouble() : 1.0;

                triggerRetrain = acc < gate.MinAccuracy || f1 < gate.MinF1 || (gate.RetrainIntervalCycles > 0 && cycle % gate.RetrainIntervalCycles == 0);
                reason = triggerRetrain
                    ? $"acc={acc:F3}<{gate.MinAccuracy} or f1={f1:F3}<{gate.MinF1} or cycle-interval"
                    : $"metrics ok acc={acc:F3} f1={f1:F3}";

                if (triggerRetrain)
                {
                    await TriggerRetrainAsync(def, symbol, logger, ct).ConfigureAwait(false);
                }

                var status = triggerRetrain ? "warn" : "ok";
                var value  = triggerRetrain ? $"retrain-triggered {reason}" : $"no-retrain {reason}";
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
        }

        return results;
    }

    private async Task TriggerRetrainAsync(WorkflowDefinition def, string symbol, IWorkflowLogger logger, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            type       = MessageTypes.TrainingJobStart,
            model_type = "model-t",
            symbol,
            trigger    = "retrain-gate",
        });
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        try
        {
            var resp = await _http.PostAsync($"{def.BlockControllerUrl}/api/training/start", content, ct).ConfigureAwait(false);
            logger.LogInfo($"Retrain triggered for {symbol}: {(int)resp.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarn($"Retrain trigger failed for {symbol}: {ex.Message}");
        }
    }
}
