using Microsoft.AspNetCore.SignalR.Client;

namespace MLS.WorkflowRunner.Engine;

/// <summary>
/// Main N-cycle workflow execution engine. Connects to Block Controller via SignalR,
/// executes workflow steps sequentially, and logs structured results.
/// </summary>
public sealed class WorkflowEngine(IWorkflowLogger _logger) : IAsyncDisposable
{
    private HubConnection? _hub;

    /// <summary>Run the workflow for the specified number of cycles.</summary>
    public async Task<IReadOnlyList<CycleResult>> RunAsync(
        WorkflowDefinition def,
        IReadOnlyList<IWorkflowStep> steps,
        CancellationToken ct = default)
    {
        _logger.LogInfo($"Connecting to Block Controller at {def.BlockControllerUrl}");

        var clientId = Guid.NewGuid();
        _hub = new HubConnectionBuilder()
            .WithUrl($"{def.BlockControllerUrl}/hubs/block-controller?clientId={clientId}")
            .WithAutomaticReconnect()
            .Build();

        _hub.On<EnvelopePayload>("ReceiveEnvelope", envelope =>
            _logger.LogInfo($"[BC] envelope type={envelope.Type} module={envelope.ModuleId}"));

        try
        {
            await _hub.StartAsync(ct).ConfigureAwait(false);
            _logger.LogInfo($"Connected to Block Controller (clientId={clientId})");
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"Could not connect to Block Controller: {ex.Message}. Continuing without hub.");
        }

        // Filter steps by definition (empty = all)
        var enabledSteps = def.Steps.Length > 0
            ? steps.Where(s => def.Steps.Contains(s.StepName, StringComparer.OrdinalIgnoreCase)).ToList()
            : steps.ToList();

        _logger.LogInfo($"Running workflow '{def.Name}': {def.Cycles} cycle(s), {enabledSteps.Count} step(s), {def.Symbols.Length} symbol(s)");

        var cycleResults = new List<CycleResult>();

        for (var cycle = 1; cycle <= def.Cycles; cycle++)
        {
            if (ct.IsCancellationRequested) break;

            _logger.LogCycleStart(cycle, def.Cycles, def);
            var cycleStart    = DateTimeOffset.UtcNow;
            var allStepResults = new List<StepResult>();
            var cycleStatus   = "completed";

            foreach (var step in enabledSteps)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var stepResults = await step.ExecuteAsync(def, cycle, _logger, ct).ConfigureAwait(false);
                    allStepResults.AddRange(stepResults);

                    // Propagate error status to cycle if any step errors out
                    if (stepResults.Any(r => r.Status == "error"))
                        cycleStatus = "failed";
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Step '{step.StepName}' threw an unexpected exception", ex);
                    cycleStatus = "failed";
                }
            }

            var cycleResult = new CycleResult(
                Cycle: cycle,
                TotalCycles: def.Cycles,
                StartedAt: cycleStart,
                CompletedAt: DateTimeOffset.UtcNow,
                StepResults: allStepResults,
                Status: cycleStatus);

            cycleResults.Add(cycleResult);
            _logger.LogCycleSummary(cycleResult);
        }

        _logger.LogFinalSummary(cycleResults, def);
        return cycleResults;
    }

    /// <summary>Creates the default ordered set of 8 workflow steps.</summary>
    public static IReadOnlyList<IWorkflowStep> CreateDefaultSteps(HttpClient http) =>
    [
        new PriceCheckStep(http),
        new ArbitrageAvailabilityStep(http),
        new MarketConditionsStep(http),
        new ModelEvaluationStep(http),
        new RetrainDecisionStep(http),
        new BlockRegistryCheckStep(http),
        new DeFiHealthCheckStep(http),
        new MTFClassifierTrainingStep(http),
    ];

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync().ConfigureAwait(false);
        }
    }
}
