using System.Text.Json.Serialization;

namespace MLS.BlockController.Models;

/// <summary>Registration record for a module connected to the Block Controller.</summary>
/// <param name="ModuleId">Unique runtime identifier assigned on registration.</param>
/// <param name="ModuleName">Human-readable module name, e.g. <c>"trader"</c>.</param>
/// <param name="EndpointHttp">HTTP base URL, e.g. <c>"http://trader:5300"</c>.</param>
/// <param name="EndpointWs">WebSocket URL, e.g. <c>"ws://trader:6300"</c>.</param>
/// <param name="Capabilities">Declared capabilities, e.g. <c>["trading", "ml-inference"]</c>.</param>
/// <param name="Version">Module semantic version string.</param>
/// <param name="RegisteredAt">UTC time of first registration.</param>
/// <param name="LastHeartbeat">UTC time of most recent heartbeat.</param>
public sealed record ModuleRegistration(
    [property: JsonPropertyName("module_id")] Guid ModuleId,
    [property: JsonPropertyName("module_name")] string ModuleName,
    [property: JsonPropertyName("endpoint_http")] string EndpointHttp,
    [property: JsonPropertyName("endpoint_ws")] string EndpointWs,
    [property: JsonPropertyName("capabilities")] IReadOnlyList<string> Capabilities,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("registered_at")] DateTimeOffset RegisteredAt,
    [property: JsonPropertyName("last_heartbeat")] DateTimeOffset LastHeartbeat);
