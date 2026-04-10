namespace MLS.WorkflowRunner.Logging;

/// <summary>
/// ANSI-coloured console logger with optional NDJSON side-file output.
/// </summary>
public sealed class ConsoleWorkflowLogger : IWorkflowLogger, IDisposable
{
    private readonly StreamWriter? _jsonWriter;
    private readonly object _lock = new();

    public ConsoleWorkflowLogger(string? jsonLogPath = null)
    {
        if (jsonLogPath is not null)
        {
            var dir = Path.GetDirectoryName(jsonLogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            _jsonWriter = new StreamWriter(jsonLogPath, append: false) { AutoFlush = true };
        }
    }

    public void LogCycleStart(int cycle, int total, WorkflowDefinition def)
    {
        lock (_lock)
        {
            WriteColored($"\n══════════════════════════════════════════════════════", ConsoleColor.DarkCyan);
            WriteColored($"  CYCLE {cycle}/{total}  ·  {def.Name}  ·  symbols={string.Join(",", def.Symbols)}", ConsoleColor.Cyan);
            WriteColored($"══════════════════════════════════════════════════════\n", ConsoleColor.DarkCyan);
        }
    }

    public void LogStepStart(int cycle, int total, string step, string symbol)
    {
        lock (_lock)
        {
            WriteColored($"[CYCLE {cycle}/{total}][STEP {step}][{symbol}] starting…", ConsoleColor.DarkGray);
        }
    }

    public void LogStepComplete(StepResult r, int cycle, int total)
    {
        var color = r.Status switch
        {
            "ok"      => ConsoleColor.Green,
            "warn"    => ConsoleColor.Yellow,
            "skipped" => ConsoleColor.DarkGray,
            _         => ConsoleColor.Red,
        };
        lock (_lock)
        {
            WriteColored($"[CYCLE {cycle}/{total}][STEP {r.Step}][{r.Symbol}] status={r.Status} latency={r.LatencyMs}ms {r.Value}", color);
            if (r.Error is not null)
                WriteColored($"  └─ error: {r.Error}", ConsoleColor.DarkRed);
        }

        WriteJsonLine(new { type = "step_result", cycle, total, r.Step, r.Symbol, r.Status, r.LatencyMs, r.Value, r.Error });
    }

    public void LogCycleSummary(CycleResult result)
    {
        lock (_lock)
        {
            WriteColored($"\n  ── Cycle {result.Cycle} Summary ──────────────────────────────────", ConsoleColor.DarkCyan);
            WriteColored($"  {"Step",-28} {"Symbol",-14} {"Status",-10} {"Latency(ms)",12}  Value", ConsoleColor.White);
            WriteColored($"  {new string('─', 80)}", ConsoleColor.DarkGray);
            foreach (var r in result.StepResults)
            {
                var color = r.Status switch { "ok" => ConsoleColor.Green, "warn" => ConsoleColor.Yellow, _ => ConsoleColor.Red };
                WriteColored($"  {r.Step,-28} {r.Symbol,-14} {r.Status,-10} {r.LatencyMs,12}  {r.Value}", color);
            }
            var elapsed = (result.CompletedAt - result.StartedAt).TotalSeconds;
            WriteColored($"\n  Cycle {result.Cycle} complete in {elapsed:F1}s  |  status={result.Status}\n", ConsoleColor.DarkCyan);
        }

        WriteJsonLine(new { type = "cycle_summary", cycle = result.Cycle, total = result.TotalCycles, status = result.Status, elapsed_s = (result.CompletedAt - result.StartedAt).TotalSeconds });
    }

    public void LogFinalSummary(IReadOnlyList<CycleResult> results, WorkflowDefinition def)
    {
        lock (_lock)
        {
            WriteColored($"\n╔══════════════════════════════════════════════════════╗", ConsoleColor.Cyan);
            WriteColored($"║  WORKFLOW COMPLETE  ·  {def.Name,-31}║", ConsoleColor.Cyan);
            WriteColored($"╚══════════════════════════════════════════════════════╝", ConsoleColor.Cyan);

            var ok      = results.Count(r => r.Status == "completed");
            var failed  = results.Count(r => r.Status != "completed");
            WriteColored($"  Cycles run:    {results.Count}", ConsoleColor.White);
            WriteColored($"  Completed:     {ok}", ConsoleColor.Green);
            WriteColored($"  Failed:        {failed}", failed > 0 ? ConsoleColor.Red : ConsoleColor.White);

            var allStepResults = results.SelectMany(c => c.StepResults).ToList();
            var byStep = allStepResults.GroupBy(r => r.Step).OrderBy(g => g.Key);
            WriteColored($"\n  Per-step summary:", ConsoleColor.White);
            foreach (var g in byStep)
            {
                var okCount  = g.Count(r => r.Status == "ok");
                var warnCount = g.Count(r => r.Status == "warn");
                var errCount = g.Count(r => r.Status == "error");
                WriteColored($"    {g.Key,-30} ok={okCount} warn={warnCount} err={errCount}", ConsoleColor.Gray);
            }
            WriteColored("", ConsoleColor.White);
        }

        WriteJsonLine(new { type = "final_summary", workflow = def.Name, cycles = results.Count, timestamp = DateTimeOffset.UtcNow });
    }

    public void LogInfo(string message)
    {
        lock (_lock) WriteColored($"[INFO] {message}", ConsoleColor.Gray);
        WriteJsonLine(new { type = "info", message, timestamp = DateTimeOffset.UtcNow });
    }

    public void LogWarn(string message)
    {
        lock (_lock) WriteColored($"[WARN] {message}", ConsoleColor.Yellow);
        WriteJsonLine(new { type = "warn", message, timestamp = DateTimeOffset.UtcNow });
    }

    public void LogError(string message, Exception? ex = null)
    {
        lock (_lock)
        {
            WriteColored($"[ERROR] {message}", ConsoleColor.Red);
            if (ex is not null)
                WriteColored($"  └─ {ex.GetType().Name}: {ex.Message}", ConsoleColor.DarkRed);
        }
        WriteJsonLine(new { type = "error", message, exception = ex?.Message, timestamp = DateTimeOffset.UtcNow });
    }

    public void Dispose() => _jsonWriter?.Dispose();

    // ── Private helpers ───────────────────────────────────────────────────────────

    private static void WriteColored(string text, ConsoleColor color)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = prev;
    }

    private void WriteJsonLine(object obj)
    {
        if (_jsonWriter is null) return;
        lock (_jsonWriter)
            _jsonWriter.WriteLine(JsonSerializer.Serialize(obj));
    }
}
