// MLS.WorkflowRunner — manual N-cycle workflow execution engine
//
// Usage:
//   dotnet run -- --workflow src/workflow-runner/workflows/market-conditions.json
//   dotnet run -- --workflow src/workflow-runner/workflows/mtf-classifier-workflow.json --cycles 5
//   dotnet run -- --workflow src/workflow-runner/workflows/defi-price-checks.json --json-log /tmp/run.ndjson

using MLS.WorkflowRunner.Engine;
using MLS.WorkflowRunner.Logging;

// ── Parse arguments ───────────────────────────────────────────────────────────

var workflowPath = GetArg("--workflow");
var jsonLogPath  = GetArgOpt("--json-log");
var cyclesOpt    = GetArgOpt("--cycles");
var bcUrlOpt     = GetArgOpt("--bc-url");

if (workflowPath is null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("Usage: MLS.WorkflowRunner --workflow <path.json> [--cycles N] [--json-log <path>] [--bc-url <url>]");
    Console.ResetColor();
    return 1;
}

if (!File.Exists(workflowPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Workflow file not found: {workflowPath}");
    Console.ResetColor();
    return 1;
}

// ── Load workflow definition ──────────────────────────────────────────────────

WorkflowDefinition def;
try
{
    var json = await File.ReadAllTextAsync(workflowPath);
    def = JsonSerializer.Deserialize<WorkflowDefinition>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
          ?? throw new InvalidOperationException("Failed to deserialize workflow definition.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Failed to load workflow: {ex.Message}");
    Console.ResetColor();
    return 1;
}

// Apply CLI overrides
if (cyclesOpt is not null && int.TryParse(cyclesOpt, out var cyclesOverride))
    def = def with { Cycles = cyclesOverride };
if (bcUrlOpt is not null)
    def = def with { BlockControllerUrl = bcUrlOpt };

// ── Print startup banner ──────────────────────────────────────────────────────

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║          MLS WorkflowRunner — N-Cycle Execution Engine       ║
╚══════════════════════════════════════════════════════════════╝");
Console.ResetColor();

Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine($"  Workflow    : {def.Name}");
Console.WriteLine($"  Cycles      : {def.Cycles}");
Console.WriteLine($"  Symbols     : {string.Join(", ", def.Symbols)}");
Console.WriteLine($"  Timeframes  : {string.Join(", ", def.Timeframes)}");
Console.WriteLine($"  Steps       : {(def.Steps.Length > 0 ? string.Join(", ", def.Steps) : "all")}");
Console.WriteLine($"  BC URL      : {def.BlockControllerUrl}");
Console.WriteLine($"  Designer URL: {def.DesignerUrl}");
Console.WriteLine($"  DataLayer   : {def.DataLayerUrl}");
if (jsonLogPath is not null)
    Console.WriteLine($"  JSON log    : {jsonLogPath}");
Console.ResetColor();

// ── Cancellation ──────────────────────────────────────────────────────────────

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// ── Build engine and run ──────────────────────────────────────────────────────

using var logger = new ConsoleWorkflowLogger(jsonLogPath);
await using var engine = new WorkflowEngine(logger);

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
var steps = WorkflowEngine.CreateDefaultSteps(http);

try
{
    await engine.RunAsync(def, steps, cts.Token);
    return 0;
}
catch (OperationCanceledException)
{
    logger.LogWarn("Workflow cancelled by user.");
    return 130;
}
catch (Exception ex)
{
    logger.LogError("Workflow terminated with an unhandled exception.", ex);
    return 1;
}

// ── CLI helpers ───────────────────────────────────────────────────────────────

static string? GetArg(string name)
{
    var args = Environment.GetCommandLineArgs();
    var idx  = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

static string? GetArgOpt(string name) => GetArg(name);
