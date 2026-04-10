using Microsoft.AspNetCore.SignalR;
using MLS.Core.Constants;

namespace MLS.Network.TaskIdGenerator.Hubs;

/// <summary>SignalR hub exposing the task-id-generator WebSocket API on port 6011.</summary>
public sealed class TaskIdGeneratorHub(
    ITaskIdService _service,
    ILogger<TaskIdGeneratorHub> _logger) : Hub<ITaskIdGeneratorHubClient>
{
    /// <summary>Generates a task ID and returns it to the caller via <c>ReceiveTaskId</c>.</summary>
    public async Task GenerateTaskId(EnvelopePayload envelope)
    {
        var moduleId = envelope.Payload.TryGetProperty("module_id", out var m)
            ? m.GetString() ?? envelope.ModuleId : envelope.ModuleId;
        var taskType = envelope.Payload.TryGetProperty("task_type", out var t)
            ? t.GetString() ?? "default" : "default";

        var id = _service.GenerateTaskId(moduleId, taskType);
        var response = EnvelopePayload.Create(
            MessageTypes.TaskIdGenerated,
            TaskIdGeneratorConstants.ModuleName,
            new { task_id = id });
        await Clients.Caller.ReceiveTaskId(response).ConfigureAwait(false);
    }

    /// <summary>Validates a task ID and returns the result to the caller.</summary>
    public async Task ValidateTaskId(EnvelopePayload envelope)
    {
        var taskId = envelope.Payload.TryGetProperty("task_id", out var t)
            ? t.GetString() ?? string.Empty : string.Empty;
        var valid      = _service.ValidateTaskId(taskId);
        var components = _service.ParseTaskId(taskId);
        var response = EnvelopePayload.Create(
            MessageTypes.TaskIdValidated,
            TaskIdGeneratorConstants.ModuleName,
            new { valid, components });
        await Clients.Caller.ReceiveTaskId(response).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "broadcast").ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}

/// <summary>Client-side methods pushed by the TaskIdGenerator hub.</summary>
public interface ITaskIdGeneratorHubClient
{
    /// <summary>Receives a task ID result or validation response.</summary>
    Task ReceiveTaskId(EnvelopePayload envelope);
}
