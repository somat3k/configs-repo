using System.Collections.Concurrent;

namespace MLS.WebApp.Components.Canvas;

/// <summary>
/// MDI layout state engine. Tracks all open <see cref="DocumentWindow"/> instances,
/// their positions, sizes, z-order, and minimise/maximise state.
/// Persists layout to localStorage via <see cref="IWindowLayoutService"/>.
/// </summary>
public sealed class WindowManager(
    IWindowLayoutService layoutService,
    ILogger<WindowManager> logger)
{
    private readonly ConcurrentDictionary<Guid, WindowState> _windows = new();
    private int _maxZIndex = 100;

    // Drag tracking
    private Guid _dragWindowId = Guid.Empty;
    private double _dragStartMouseX;
    private double _dragStartMouseY;
    private double _dragStartWinX;
    private double _dragStartWinY;

    // Resize tracking
    private Guid _resizeWindowId = Guid.Empty;
    private ResizeDirection _resizeDirection;
    private double _resizeStartMouseX;
    private double _resizeStartMouseY;
    private double _resizeStartWinX;
    private double _resizeStartWinY;
    private double _resizeStartWidth;
    private double _resizeStartHeight;

    private const double MinWidth = 280;
    private const double MinHeight = 180;

    /// <summary>Fires whenever any window state changes; triggers CanvasHost re-render.</summary>
    public event Action? OnLayoutChanged;

    /// <summary>Read-only snapshot of all currently open windows.</summary>
    public IReadOnlyCollection<WindowState> Windows => _windows.Values.ToList().AsReadOnly();

    /// <summary>
    /// Opens a new panel window. Returns the window ID so callers can track it.
    /// </summary>
    /// <param name="panelType">Logical panel type key (e.g. "TradingTerminal").</param>
    /// <param name="title">Optional display title; defaults to <paramref name="panelType"/>.</param>
    /// <param name="x">Initial left position in px; default 80.</param>
    /// <param name="y">Initial top position in px; default 60.</param>
    /// <param name="width">Initial width in px; default 800.</param>
    /// <param name="height">Initial height in px; default 520.</param>
    public Guid OpenPanel(string panelType, string? title = null,
        double x = 80, double y = 60, double width = 800, double height = 520)
    {
        var id = Guid.NewGuid();
        var state = new WindowState(id, panelType, title ?? panelType,
            X: x + _windows.Count * 24,
            Y: y + _windows.Count * 24,
            Width: width,
            Height: height,
            ZIndex: ++_maxZIndex,
            IsMinimized: false,
            IsMaximized: false);

        _windows[id] = state;
        logger.LogDebug("Opened panel {PanelType} id={Id}", panelType, id);
        RaiseLayoutChanged();
        return id;
    }

    /// <summary>Closes and removes a window by ID.</summary>
    public void Close(Guid windowId)
    {
        if (_windows.TryRemove(windowId, out _))
        {
            logger.LogDebug("Closed window {Id}", windowId);
            RaiseLayoutChanged();
        }
    }

    /// <summary>Minimises a window to the taskbar.</summary>
    public void Minimize(Guid windowId)
    {
        if (_windows.TryGetValue(windowId, out var s))
        {
            _windows[windowId] = s with { IsMinimized = true, IsMaximized = false };
            RaiseLayoutChanged();
        }
    }

    /// <summary>Restores a minimised window back to its previous size and position.</summary>
    public void Restore(Guid windowId)
    {
        if (_windows.TryGetValue(windowId, out var s))
        {
            _windows[windowId] = s with { IsMinimized = false, IsMaximized = false };
            BringToFront(windowId);
        }
    }

    /// <summary>Toggles maximised state, filling the entire canvas container.</summary>
    public void Maximize(Guid windowId)
    {
        if (_windows.TryGetValue(windowId, out var s))
        {
            _windows[windowId] = s with
            {
                IsMaximized = !s.IsMaximized,
                IsMinimized = false,
            };
            BringToFront(windowId);
        }
    }

    /// <summary>Raises the window to the top of the z-stack.</summary>
    public void BringToFront(Guid windowId)
    {
        if (_windows.TryGetValue(windowId, out var s))
        {
            _windows[windowId] = s with { ZIndex = ++_maxZIndex };
            RaiseLayoutChanged();
        }
    }

    // ── Drag ─────────────────────────────────────────────────────────────────

    /// <summary>Records the start of a title-bar drag for a window.</summary>
    public void StartDrag(Guid windowId, double mouseX, double mouseY)
    {
        if (!_windows.TryGetValue(windowId, out var s)) return;
        if (s.IsMaximized) return;

        _dragWindowId  = windowId;
        _dragStartMouseX = mouseX;
        _dragStartMouseY = mouseY;
        _dragStartWinX = s.X;
        _dragStartWinY = s.Y;
        BringToFront(windowId);
    }

    /// <summary>Updates window position during an active drag.</summary>
    public void ContinueDrag(double mouseX, double mouseY)
    {
        if (_dragWindowId == Guid.Empty) return;
        if (!_windows.TryGetValue(_dragWindowId, out var s)) return;

        var dx = mouseX - _dragStartMouseX;
        var dy = mouseY - _dragStartMouseY;

        _windows[_dragWindowId] = s with
        {
            X = Math.Max(0, _dragStartWinX + dx),
            Y = Math.Max(0, _dragStartWinY + dy),
        };
        RaiseLayoutChanged();
    }

    /// <summary>Finalises the drag and persists the new layout.</summary>
    public void EndDrag() => _dragWindowId = Guid.Empty;

    // ── Resize ───────────────────────────────────────────────────────────────

    /// <summary>Begins a resize operation in the given direction.</summary>
    public void StartResize(Guid windowId, ResizeDirection direction, double mouseX, double mouseY)
    {
        if (!_windows.TryGetValue(windowId, out var s)) return;

        _resizeWindowId    = windowId;
        _resizeDirection   = direction;
        _resizeStartMouseX = mouseX;
        _resizeStartMouseY = mouseY;
        _resizeStartWinX   = s.X;
        _resizeStartWinY   = s.Y;
        _resizeStartWidth  = s.Width;
        _resizeStartHeight = s.Height;
        BringToFront(windowId);
    }

    /// <summary>Updates window dimensions during an active resize.</summary>
    public void ContinueResize(double mouseX, double mouseY)
    {
        if (_resizeWindowId == Guid.Empty) return;
        if (!_windows.TryGetValue(_resizeWindowId, out var s)) return;

        var dx = mouseX - _resizeStartMouseX;
        var dy = mouseY - _resizeStartMouseY;

        var (newX, newY, newW, newH) = (_resizeStartWinX, _resizeStartWinY,
                                        _resizeStartWidth, _resizeStartHeight);

        if (_resizeDirection is ResizeDirection.E or ResizeDirection.NE or ResizeDirection.SE)
            newW = Math.Max(MinWidth, _resizeStartWidth + dx);

        if (_resizeDirection is ResizeDirection.W or ResizeDirection.NW or ResizeDirection.SW)
        {
            newW = Math.Max(MinWidth, _resizeStartWidth - dx);
            newX = _resizeStartWinX + (_resizeStartWidth - newW);
        }

        if (_resizeDirection is ResizeDirection.S or ResizeDirection.SE or ResizeDirection.SW)
            newH = Math.Max(MinHeight, _resizeStartHeight + dy);

        if (_resizeDirection is ResizeDirection.N or ResizeDirection.NE or ResizeDirection.NW)
        {
            newH = Math.Max(MinHeight, _resizeStartHeight - dy);
            newY = _resizeStartWinY + (_resizeStartHeight - newH);
        }

        _windows[_resizeWindowId] = s with { X = newX, Y = newY, Width = newW, Height = newH };
        RaiseLayoutChanged();
    }

    /// <summary>Finalises the resize.</summary>
    public void EndResize() => _resizeWindowId = Guid.Empty;

    // ── Layout persistence ────────────────────────────────────────────────────

    /// <summary>Persists the current window layout to localStorage.</summary>
    public Task SaveLayoutAsync(CancellationToken ct)
        => layoutService.SaveAsync(_windows.Values, ct);

    /// <summary>Restores previously saved window layout from localStorage on page load.</summary>
    public async Task RestoreLayoutAsync(CancellationToken ct)
    {
        var saved = await layoutService.LoadAsync(ct).ConfigureAwait(false);
        foreach (var w in saved)
        {
            _windows[w.WindowId] = w;
            if (w.ZIndex > _maxZIndex) _maxZIndex = w.ZIndex;
        }
        if (saved.Length > 0)
        {
            logger.LogInformation("Restored {Count} windows from localStorage", saved.Length);
            RaiseLayoutChanged();
        }
    }

    private void RaiseLayoutChanged() => OnLayoutChanged?.Invoke();
}

/// <summary>Snapshot of a single MDI window's layout and state.</summary>
/// <param name="WindowId">Unique window instance ID.</param>
/// <param name="PanelType">Logical panel type (used to render the correct component).</param>
/// <param name="Title">Title bar display text.</param>
/// <param name="X">Left position in px relative to the CanvasHost container.</param>
/// <param name="Y">Top position in px relative to the CanvasHost container.</param>
/// <param name="Width">Window width in px.</param>
/// <param name="Height">Window height in px.</param>
/// <param name="ZIndex">CSS z-index; higher values appear on top.</param>
/// <param name="IsMinimized">True when collapsed to taskbar.</param>
/// <param name="IsMaximized">True when expanded to fill the canvas container.</param>
public sealed record WindowState(
    Guid WindowId,
    string PanelType,
    string Title,
    double X,
    double Y,
    double Width,
    double Height,
    int ZIndex,
    bool IsMinimized,
    bool IsMaximized);

/// <summary>Eight directional resize handles for a DocumentWindow.</summary>
public enum ResizeDirection { N, NE, E, SE, S, SW, W, NW }
