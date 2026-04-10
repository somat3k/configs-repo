namespace MLS.WorkflowRunner.Steps;

/// <summary>Contract for a workflow step that executes per symbol/timeframe.</summary>
public interface IWorkflowStep
{
    /// <summary>Unique name identifying this step.</summary>
    string StepName { get; }

    /// <summary>Execute the step for all symbols in the definition.</summary>
    Task<IReadOnlyList<StepResult>> ExecuteAsync(
        WorkflowDefinition def,
        int cycle,
        IWorkflowLogger logger,
        CancellationToken ct);
}
