using System.Text.Json.Serialization;

namespace MLS.Designer.Services;

/// <summary>
/// Metadata record returned by <see cref="IBlockRegistry.GetAll"/> and exposed via
/// <c>GET /api/blocks</c>.
/// </summary>
/// <param name="Key">Registry key, e.g. <c>"RSIBlock"</c>.</param>
/// <param name="DisplayName">Human-readable name shown in the canvas palette.</param>
/// <param name="Category">Block category, e.g. <c>"Indicator"</c>, <c>"ML"</c>, <c>"Risk"</c>.</param>
/// <param name="Description">Short description shown as tooltip in the Designer UI.</param>
/// <param name="InputSocketNames">Names of input sockets on this block type.</param>
/// <param name="OutputSocketNames">Names of output sockets on this block type.</param>
public sealed record BlockMetadata(
    [property: JsonPropertyName("key")]                  string Key,
    [property: JsonPropertyName("display_name")]         string DisplayName,
    [property: JsonPropertyName("category")]             string Category,
    [property: JsonPropertyName("description")]          string Description,
    [property: JsonPropertyName("input_socket_names")]   IReadOnlyList<string> InputSocketNames,
    [property: JsonPropertyName("output_socket_names")]  IReadOnlyList<string> OutputSocketNames);
