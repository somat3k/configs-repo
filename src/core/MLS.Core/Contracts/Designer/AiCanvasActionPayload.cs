using System.Text.Json;
using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>AI_CANVAS_ACTION</c> — dispatched by ai-hub Semantic Kernel plugins
/// to direct the web-app MDI canvas. Actions MUST be dispatched BEFORE the text chunk response.
/// </summary>
/// <param name="ActionType">
/// The canvas action type: <c>OpenPanel</c>, <c>UpdateChart</c>, <c>HighlightBlock</c>,
/// <c>ShowDiagram</c>, <c>AddAnnotation</c>, <c>OpenDesignerGraph</c>.
/// </param>
/// <param name="PanelType">Target panel type for <c>OpenPanel</c> actions (e.g. <c>"TradingChart"</c>).</param>
/// <param name="Data">Action-specific payload (JSON object).</param>
/// <param name="Title">Optional window title for new MDI panels.</param>
public sealed record AiCanvasActionPayload(
    [property: JsonPropertyName("action_type")] string ActionType,
    [property: JsonPropertyName("panel_type")]  string? PanelType,
    [property: JsonPropertyName("data")]        JsonElement Data,
    [property: JsonPropertyName("title")]       string? Title);
