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
    /// <remarks>
    /// Validates that the task ID:
    /// <list type="bullet">
    ///   <item>starts with <c>task:</c></item>
    ///   <item>splits into exactly 5 colon-separated parts</item>
    ///   <item>part [3] is a 16-digit timestamp (<c>yyyyMMddHHmmssff</c>)</item>
    ///   <item>part [4] is a non-negative integer sequence number</item>
    /// </list>
    /// Note: <c>moduleId</c> or <c>taskType</c> values containing <c>:</c> will cause
    /// validation to return <see langword="false"/>; avoid colons in those fields.
    /// </remarks>
    public bool ValidateTaskId(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return false;
        if (!taskId.StartsWith("task:", StringComparison.Ordinal)) return false;
        var parts = taskId.Split(':');
        if (parts.Length != 5) return false;
        // Validate timestamp part is exactly 16 digits (yyyyMMddHHmmssff)
        var timestamp = parts[3];
        if (timestamp.Length != 16 || !timestamp.All(char.IsAsciiDigit)) return false;
        // Validate sequence part is a non-negative integer
        return long.TryParse(parts[4], out var seq) && seq >= 0;
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
