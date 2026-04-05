using System.Text.Json;
using System.Text.Json.Serialization;

namespace MLS.AIHub.Canvas;

/// <summary>
/// Discriminated union base type for all canvas actions that the AI Hub
/// dispatches to the web-app MDI canvas via <c>AI_CANVAS_ACTION</c> envelopes.
/// </summary>
public abstract record CanvasAction
{
    /// <summary>Action type discriminator string used in envelope serialisation.</summary>
    [JsonPropertyName("action_type")]
    public abstract string ActionType { get; }
}

/// <summary>Opens a new MDI panel of the specified type on the canvas.</summary>
/// <param name="PanelType">
/// Panel type identifier, e.g. <c>TradingChart</c>, <c>SHAPPlot</c>, <c>StrategyGraph</c>.
/// </param>
/// <param name="Data">Panel-specific data payload (JSON object).</param>
/// <param name="Title">Optional window title. Defaults to <paramref name="PanelType"/> when omitted.</param>
public sealed record OpenPanelAction(
    [property: JsonPropertyName("panel_type")] string PanelType,
    [property: JsonPropertyName("data")] JsonElement Data,
    [property: JsonPropertyName("title")] string? Title = null) : CanvasAction
{
    /// <inheritdoc/>
    public override string ActionType => "OpenPanel";
}

/// <summary>Updates an existing chart series on the canvas.</summary>
/// <param name="ChartId">Target chart panel identifier.</param>
/// <param name="SeriesName">Name of the data series to update.</param>
/// <param name="Values">New value array for the series.</param>
/// <param name="Timestamps">Corresponding UTC timestamps for each value.</param>
public sealed record UpdateChartAction(
    [property: JsonPropertyName("chart_id")] Guid ChartId,
    [property: JsonPropertyName("series_name")] string SeriesName,
    [property: JsonPropertyName("values")] double[] Values,
    [property: JsonPropertyName("timestamps")] DateTimeOffset[] Timestamps) : CanvasAction
{
    /// <inheritdoc/>
    public override string ActionType => "UpdateChart";
}

/// <summary>Highlights a block in the Designer canvas for the specified duration.</summary>
/// <param name="BlockId">Target block identifier.</param>
/// <param name="Color">CSS colour string (e.g. <c>#00d4ff</c>).</param>
/// <param name="DurationMs">Highlight duration in milliseconds. Default 2000 ms.</param>
public sealed record HighlightBlockAction(
    [property: JsonPropertyName("block_id")] Guid BlockId,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("duration_ms")] int DurationMs = 2000) : CanvasAction
{
    /// <inheritdoc/>
    public override string ActionType => "HighlightBlock";
}

/// <summary>Renders a Mermaid diagram in a new canvas panel.</summary>
/// <param name="MermaidSource">Mermaid diagram source text.</param>
/// <param name="Title">Panel window title.</param>
public sealed record ShowDiagramAction(
    [property: JsonPropertyName("mermaid_source")] string MermaidSource,
    [property: JsonPropertyName("title")] string Title) : CanvasAction
{
    /// <inheritdoc/>
    public override string ActionType => "ShowDiagram";
}

/// <summary>Adds a time-based annotation marker to an existing chart.</summary>
/// <param name="ChartId">Target chart panel identifier.</param>
/// <param name="Time">UTC timestamp for the annotation.</param>
/// <param name="Label">Annotation label text.</param>
/// <param name="Color">CSS colour string. Defaults to MLS accent colour.</param>
public sealed record AddAnnotationAction(
    [property: JsonPropertyName("chart_id")] Guid ChartId,
    [property: JsonPropertyName("time")] DateTimeOffset Time,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("color")] string Color = "#00d4ff") : CanvasAction
{
    /// <inheritdoc/>
    public override string ActionType => "AddAnnotation";
}

/// <summary>Opens a strategy graph in the Designer canvas.</summary>
/// <param name="StrategySchema">Full strategy graph schema as a JSON element.</param>
public sealed record OpenDesignerGraphAction(
    [property: JsonPropertyName("strategy_schema")] JsonElement StrategySchema) : CanvasAction
{
    /// <inheritdoc/>
    public override string ActionType => "OpenDesignerGraph";
}
