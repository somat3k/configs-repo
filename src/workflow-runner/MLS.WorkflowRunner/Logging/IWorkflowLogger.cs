namespace MLS.WorkflowRunner.Logging;

/// <summary>Abstraction for workflow event logging.</summary>
public interface IWorkflowLogger
{
    void LogCycleStart(int cycle, int total, WorkflowDefinition def);
    void LogStepStart(int cycle, int total, string step, string symbol);
    void LogStepComplete(StepResult result, int cycle, int total);
    void LogCycleSummary(CycleResult result);
    void LogFinalSummary(IReadOnlyList<CycleResult> results, WorkflowDefinition def);
    void LogInfo(string message);
    void LogWarn(string message);
    void LogError(string message, Exception? ex = null);
}
