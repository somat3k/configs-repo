namespace MLS.WorkflowRunner.Steps;

/// <summary>
/// Calls the Designer module's block registry API and logs all registered block types by category.
/// </summary>
public sealed class BlockRegistryCheckStep(HttpClient _http) : IWorkflowStep
{
    public string StepName => "block-registry-check";

    public async Task<IReadOnlyList<StepResult>> ExecuteAsync(
        WorkflowDefinition def, int cycle, IWorkflowLogger logger, CancellationToken ct)
    {
        var results = new List<StepResult>();
        logger.LogStepStart(cycle, def.Cycles, StepName, "designer");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var url  = $"{def.DesignerUrl}/api/blocks";
            var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var r = new StepResult(StepName, "designer", "warn", sw.ElapsedMilliseconds, "http-error", $"{(int)resp.StatusCode}");
                results.Add(r);
                logger.LogStepComplete(r, cycle, def.Cycles);
                return results;
            }

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Response is expected to be an array of block descriptors with "type" and "category" fields
            var byCategory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var block in root.EnumerateArray())
            {
                var type     = block.TryGetProperty("type",     out var tv) ? tv.GetString() ?? "unknown" : "unknown";
                var category = block.TryGetProperty("category", out var cv) ? cv.GetString() ?? "other"   : "other";
                if (!byCategory.TryGetValue(category, out var list))
                    byCategory[category] = list = [];
                list.Add(type);
            }

            foreach (var (category, types) in byCategory.OrderBy(k => k.Key))
            {
                var value  = $"count={types.Count} blocks=[{string.Join(",", types)}]";
                var result = new StepResult(StepName, category, "ok", sw.ElapsedMilliseconds, value);
                results.Add(result);
                logger.LogStepComplete(result, cycle, def.Cycles);
            }

            if (results.Count == 0)
            {
                var r = new StepResult(StepName, "designer", "warn", sw.ElapsedMilliseconds, "no-blocks-registered");
                results.Add(r);
                logger.LogStepComplete(r, cycle, def.Cycles);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            var r = new StepResult(StepName, "designer", "warn", sw.ElapsedMilliseconds, "unavailable", ex.Message);
            results.Add(r);
            logger.LogStepComplete(r, cycle, def.Cycles);
        }

        return results;
    }
}
