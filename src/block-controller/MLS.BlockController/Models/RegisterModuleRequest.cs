using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MLS.BlockController.Models;

/// <summary>Request body for <c>POST /api/modules/register</c>.</summary>
/// <param name="ModuleName">Module identifier, e.g. <c>"trader"</c>.</param>
/// <param name="EndpointHttp">HTTP base URL.</param>
/// <param name="EndpointWs">WebSocket URL.</param>
/// <param name="Capabilities">Capability strings the module advertises.</param>
/// <param name="Version">Module version string.</param>
public sealed record RegisterModuleRequest(
    [property: JsonPropertyName("module_name"),   Required] string ModuleName,
    [property: JsonPropertyName("endpoint_http"), Required] string EndpointHttp,
    [property: JsonPropertyName("endpoint_ws"),   Required] string EndpointWs,
    [property: JsonPropertyName("capabilities")]  IReadOnlyList<string> Capabilities,
    [property: JsonPropertyName("version"),       Required] string Version);
