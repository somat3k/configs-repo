namespace MLS.Network.TaskIdGenerator.Services;

/// <summary>Decomposed components of a parsed task ID.</summary>
public sealed record TaskIdComponents(
    string ModuleId,
    string TaskType,
    string Timestamp,
    long SequenceNumber);

/// <summary>Service interface for task ID generation and validation.</summary>
public interface ITaskIdService
{
    /// <summary>Generates a unique task ID for the given module and task type.</summary>
    /// <param name="moduleId">The originating module identifier.</param>
    /// <param name="taskType">The category of task being created.</param>
    string GenerateTaskId(string moduleId, string taskType);

    /// <summary>Validates that a task ID conforms to the expected format.</summary>
    /// <param name="taskId">The task ID string to validate.</param>
    bool ValidateTaskId(string taskId);

    /// <summary>Parses a task ID into its constituent components, or returns <see langword="null"/> if invalid.</summary>
    /// <param name="taskId">The task ID string to parse.</param>
    TaskIdComponents? ParseTaskId(string taskId);
}
