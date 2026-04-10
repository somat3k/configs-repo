using System.Collections.Concurrent;

namespace MLS.Network.TaskIdGenerator.Services;

/// <summary>
/// Thread-safe implementation of <see cref="ITaskIdService"/>.
/// Format: <c>task:{moduleId}:{taskType}:{yyyyMMddHHmmssff}:{counter}</c>
/// </summary>
public sealed class TaskIdService : ITaskIdService
{
    private readonly ConcurrentDictionary<string, long> _counters = new();

    /// <inheritdoc/>
    public string GenerateTaskId(string moduleId, string taskType)
    {
        var key       = $"{moduleId}:{taskType}";
        var counter   = _counters.AddOrUpdate(key, 1L, (_, prev) => Interlocked.Increment(ref prev));
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssff");
        return $"task:{moduleId}:{taskType}:{timestamp}:{counter}";
    }

    /// <inheritdoc/>
    public bool ValidateTaskId(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return false;
        if (!taskId.StartsWith("task:", StringComparison.Ordinal)) return false;
        var parts = taskId.Split(':');
        return parts.Length == 5;
    }

    /// <inheritdoc/>
    public TaskIdComponents? ParseTaskId(string taskId)
    {
        if (!ValidateTaskId(taskId)) return null;
        var parts = taskId.Split(':');
        return long.TryParse(parts[4], out var seq)
            ? new TaskIdComponents(parts[1], parts[2], parts[3], seq)
            : null;
    }
}
