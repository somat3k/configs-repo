namespace MLS.WorkflowRunner.Steps;

/// <summary>
/// Fetches OHLCV data per (symbol, timeframe) and classifies market regime via RSI.
/// </summary>
public sealed class MarketConditionsStep(HttpClient _http) : IWorkflowStep
{
    public string StepName => "market-conditions";

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
                    var url  = $"{def.DataLayerUrl}/api/market/candles?symbol={symbol}&timeframe={tf}&limit=50";
                    var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
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
                    var candles = doc.RootElement.TryGetProperty("candles", out var cv)
                        ? cv.EnumerateArray().Select(c => c.TryGetProperty("close", out var cl) ? cl.GetDouble() : 0.0).ToArray()
                        : [];

                    var rsi   = ComputeRsi14(candles);
                    var regime = rsi switch
                    {
                        > 60 => "trending-up",
                        < 40 => "trending-down",
                        _    => "ranging",
                    };

                    var value  = $"rsi={rsi:F1} regime={regime}";
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

    // ── RSI (14-period) approximation ────────────────────────────────────────────

    private static double ComputeRsi14(double[] closes)
    {
        const int period = 14;
        if (closes.Length < period + 1) return 50.0;

        var gains  = 0.0;
        var losses = 0.0;
        for (var i = closes.Length - period; i < closes.Length; i++)
        {
            var delta = closes[i] - closes[i - 1];
            if (delta > 0) gains  += delta;
            else           losses -= delta;
        }

        var avgGain = gains  / period;
        var avgLoss = losses / period;

        if (avgLoss == 0) return 100.0;
        var rs = avgGain / avgLoss;
        return 100.0 - 100.0 / (1.0 + rs);
    }
}
