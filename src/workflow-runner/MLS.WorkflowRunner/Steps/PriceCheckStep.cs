namespace MLS.WorkflowRunner.Steps;

/// <summary>
/// Fetches best bid/ask prices for each symbol from the Data Layer.
/// Sends an EXCHANGE_PRICE_UPDATE envelope and logs spread.
/// </summary>
public sealed class PriceCheckStep(HttpClient _http) : IWorkflowStep
{
    public string StepName => "price-check";

    public async Task<IReadOnlyList<StepResult>> ExecuteAsync(
        WorkflowDefinition def, int cycle, IWorkflowLogger logger, CancellationToken ct)
    {
        var results = new List<StepResult>();

        foreach (var symbol in def.Symbols)
        {
            logger.LogStepStart(cycle, def.Cycles, StepName, symbol);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var url  = $"{def.DataLayerUrl}/api/market/prices?symbol={symbol}";
                var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
                sw.Stop();

                if (!resp.IsSuccessStatusCode)
                {
                    var r = new StepResult(StepName, symbol, "warn", sw.ElapsedMilliseconds, "http-error", $"{(int)resp.StatusCode}");
                    results.Add(r);
                    logger.LogStepComplete(r, cycle, def.Cycles);
                    continue;
                }

                var json  = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var doc   = JsonDocument.Parse(json);
                var root  = doc.RootElement;
                var bid   = root.TryGetProperty("bid",  out var bv) ? bv.GetDecimal() : 0m;
                var ask   = root.TryGetProperty("ask",  out var av) ? av.GetDecimal() : 0m;
                var mid   = bid > 0 && ask > 0 ? (bid + ask) / 2m : 0m;
                var spread = mid > 0 ? (ask - bid) / mid * 100m : 0m;

                var value = $"bid={bid} ask={ask} spread={spread:F4}%";
                var result = new StepResult(StepName, symbol, "ok", sw.ElapsedMilliseconds, value);
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
}
