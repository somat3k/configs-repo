namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>CANVAS_LAYOUT_SAVE</c> — persists the current MDI canvas window
/// arrangement so it can be restored on next login.
/// </summary>
/// <param name="UserId">User whose layout is being saved.</param>
/// <param name="Layout">Ordered list of MDI window states.</param>
public sealed record CanvasLayoutSavePayload(
    Guid UserId,
    IReadOnlyList<MdiWindowState> Layout);

/// <summary>State snapshot of a single MDI window on the canvas.</summary>
/// <param name="WindowId">Unique window instance identifier.</param>
/// <param name="PanelType">Panel type, e.g. <c>"TradingChart"</c>, <c>"DesignerCanvas"</c>.</param>
/// <param name="Title">Window title bar text.</param>
/// <param name="X">Left offset in canvas pixels.</param>
/// <param name="Y">Top offset in canvas pixels.</param>
/// <param name="Width">Window width in canvas pixels.</param>
/// <param name="Height">Window height in canvas pixels.</param>
/// <param name="ZIndex">Stack order (higher = on top).</param>
/// <param name="IsMinimized">Whether the window is minimised to the taskbar.</param>
/// <param name="IsMaximized">Whether the window fills the canvas.</param>
public sealed record MdiWindowState(
    Guid WindowId,
    string PanelType,
    string Title,
    int X,
    int Y,
    int Width,
    int Height,
    int ZIndex,
    bool IsMinimized,
    bool IsMaximized);
