namespace MLS.WorkflowRunner.Steps;

/// <summary>
/// MTF Classifier training step.
/// For each (symbol, timeframe) combination emits DATA_COLLECTION_START and MTF_TRAINING_JOB_START.
/// Then fires an ensemble training job combining all timeframes.
/// </summary>
public sealed class MTFClassifierTrainingStep(HttpClient _http) : IWorkflowStep
{
    public string StepName => "mtf-classifier-training";

    public async Task<IReadOnlyList<StepResult>> ExecuteAsync(
        WorkflowDefinition def, int cycle, IWorkflowLogger logger, CancellationToken ct)
    {
        var results = new List<StepResult>();

        // ── Per-(symbol, timeframe) training jobs ─────────────────────────────────
        foreach (var symbol in def.Symbols)
        {
            foreach (var tf in def.Timeframes)
            {
                var label = $"{symbol}@{tf}";
                logger.LogStepStart(cycle, def.Cycles, StepName, label);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    // Step 1: emit DATA_COLLECTION_START
                    await EmitEnvelopeAsync(def, MessageTypes.DataCollectionStart, new
                    {
                        symbol,
                        timeframe = tf,
                        source    = "mtf-classifier-training",
                    }, ct).ConfigureAwait(false);

                    // Step 2: emit MTF_TRAINING_JOB_START for this (symbol, tf) pair
                    await EmitEnvelopeAsync(def, MessageTypes.MTFTrainingJobStart, new
                    {
                        model_type = "model-t",
                        symbol,
                        timeframe  = tf,
                        ensemble   = false,
                    }, ct).ConfigureAwait(false);

                    sw.Stop();
                    var result = new StepResult(StepName, label, "ok", sw.ElapsedMilliseconds, "job-submitted");
                    results.Add(result);
                    logger.LogStepComplete(result, cycle, def.Cycles);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    sw.Stop();
                    var r = new StepResult(StepName, label, "warn", sw.ElapsedMilliseconds, "job-failed", ex.Message);
                    results.Add(r);
                    logger.LogStepComplete(r, cycle, def.Cycles);
                }
            }
        }

        // ── Ensemble MTF Classifier job ───────────────────────────────────────────
        var ensembleLabel = $"ensemble@{string.Join("+", def.Timeframes)}";
        logger.LogStepStart(cycle, def.Cycles, StepName, ensembleLabel);
        var ensembleSw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await EmitEnvelopeAsync(def, MessageTypes.MTFTrainingJobStart, new
            {
                model_type   = "model-t",
                ensemble     = true,
                mtf_symbols  = def.Symbols,
                mtf_timeframes = def.Timeframes,
            }, ct).ConfigureAwait(false);

            ensembleSw.Stop();
            var result = new StepResult(StepName, ensembleLabel, "ok", ensembleSw.ElapsedMilliseconds, "ensemble-job-submitted");
            results.Add(result);
            logger.LogStepComplete(result, cycle, def.Cycles);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ensembleSw.Stop();
            var r = new StepResult(StepName, ensembleLabel, "warn", ensembleSw.ElapsedMilliseconds, "ensemble-job-failed", ex.Message);
            results.Add(r);
            logger.LogStepComplete(r, cycle, def.Cycles);
        }

        return results;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task EmitEnvelopeAsync(WorkflowDefinition def, string type, object payload, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            type,
            version    = 1,
            module_id  = "workflow-runner",
            timestamp  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            payload,
        });
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        await _http.PostAsync($"{def.BlockControllerUrl}/api/envelopes", content, ct).ConfigureAwait(false);
    }
}
