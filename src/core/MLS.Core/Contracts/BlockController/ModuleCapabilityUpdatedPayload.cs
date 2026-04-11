using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.BlockController;

/// <summary>
/// Payload emitted when a module's capability declaration is first stored or updated.
/// </summary>
/// <param name="ModuleId">ID of the module whose capabilities changed.</param>
/// <param name="ModuleName">Human-readable name of the module.</param>
/// <param name="OperationTypes">Envelope type values the module now handles.</param>
/// <param name="Version">Version of the capability declaration.</param>
/// <param name="Timestamp">UTC time of the update.</param>
public sealed record ModuleCapabilityUpdatedPayload(
    [property: JsonPropertyName("module_id")] Guid ModuleId,
    [property: JsonPropertyName("module_name")] string ModuleName,
    [property: JsonPropertyName("operation_types")] IReadOnlyList<string> OperationTypes,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
