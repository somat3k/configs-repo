using System.Text.Json.Serialization;

namespace MLS.BlockController.Models;

/// <summary>
/// Structured capability record stored in <see cref="MLS.BlockController.Services.ICapabilityRegistry"/>.
/// Populated from <see cref="RegisterModuleRequest.Capabilities"/> at registration time.
/// </summary>
/// <param name="ModuleId">Unique runtime identifier of the declaring module.</param>
/// <param name="ModuleName">Human-readable module name.</param>
/// <param name="OperationTypes">Envelope <c>Type</c> values this module handles.</param>
/// <param name="TensorClassesIn">BCG tensor classes the module accepts as input.</param>
/// <param name="TensorClassesOut">BCG tensor classes the module produces as output.</param>
/// <param name="TransportInterfaces">Supported transport interfaces, e.g. <c>["websocket", "http"]</c>.</param>
/// <param name="BatchSupport">Batch execution support level: <c>none</c>, <c>sequential</c>, <c>parallel</c>, <c>pipeline</c>.</param>
/// <param name="StreamingSupport">Streaming support: <c>none</c>, <c>input-only</c>, <c>output-only</c>, <c>bidirectional</c>.</param>
/// <param name="IsStateful">Whether the module maintains state across requests.</param>
/// <param name="Version">Semantic version of the capability declaration.</param>
/// <param name="RegisteredAt">UTC time this record was first created.</param>
/// <param name="LastUpdatedAt">UTC time this record was last modified.</param>
public sealed record CapabilityRecord(
    [property: JsonPropertyName("module_id")]           Guid ModuleId,
    [property: JsonPropertyName("module_name")]         string ModuleName,
    [property: JsonPropertyName("operation_types")]     IReadOnlyList<string> OperationTypes,
    [property: JsonPropertyName("tensor_classes_in")]   IReadOnlyList<string> TensorClassesIn,
    [property: JsonPropertyName("tensor_classes_out")]  IReadOnlyList<string> TensorClassesOut,
    [property: JsonPropertyName("transport_interfaces")] IReadOnlyList<string> TransportInterfaces,
    [property: JsonPropertyName("batch_support")]       string BatchSupport,
    [property: JsonPropertyName("streaming_support")]   string StreamingSupport,
    [property: JsonPropertyName("is_stateful")]         bool IsStateful,
    [property: JsonPropertyName("version")]             string Version,
    [property: JsonPropertyName("registered_at")]       DateTimeOffset RegisteredAt,
    [property: JsonPropertyName("last_updated_at")]     DateTimeOffset LastUpdatedAt)
{
    /// <summary>
    /// Build a <see cref="CapabilityRecord"/> from a module registration request.
    /// Capability strings are mapped directly to operation types; tensor/transport
    /// fields default to empty until structured declarations are adopted (Session 04).
    /// </summary>
    public static CapabilityRecord FromRegistration(Guid moduleId, string moduleName, IReadOnlyList<string> capabilities)
    {
        var now = DateTimeOffset.UtcNow;
        return new CapabilityRecord(
            ModuleId: moduleId,
            ModuleName: moduleName,
            OperationTypes: capabilities,
            TensorClassesIn: [],
            TensorClassesOut: [],
            TransportInterfaces: ["websocket", "http"],
            BatchSupport: "none",
            StreamingSupport: "none",
            IsStateful: false,
            Version: "1.0.0",
            RegisteredAt: now,
            LastUpdatedAt: now);
    }
}
