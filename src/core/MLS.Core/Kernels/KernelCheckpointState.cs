using System.Text.Json;

namespace MLS.Core.Kernels;

/// <summary>
/// Persistable checkpoint payload for stateful kernels.
/// </summary>
/// <param name="Version">Checkpoint schema version.</param>
/// <param name="CapturedAt">UTC capture timestamp.</param>
/// <param name="Payload">Serialized checkpoint payload.</param>
public sealed record KernelCheckpointState(
    string Version,
    DateTimeOffset CapturedAt,
    JsonElement Payload);
