# web-app — Session 11: Window Manager + DesignerCanvas Core

> Use this document as context when generating Web App module code with GitHub Copilot.

---

## 11. Window Manager + DesignerCanvas Core

**Phase**: 3 — MDI Canvas Rewrite

**Objective**: Implement the MDI window manager and the core DesignerCanvas component with block palette, drag-and-drop, and connection drawing.

---

### Files Created

| Action | File | Description |
|--------|------|-------------|
| CREATE | `src/web-app/WebApp/Components/Canvas/CanvasHost.razor` | MDI root: manages all open DocumentWindows |
| CREATE | `src/web-app/WebApp/Components/Canvas/DocumentWindow.razor` | Floating, resizable, dockable panel |
| CREATE | `src/web-app/WebApp/Components/Canvas/WindowManager.cs` | Layout state: positions, sizes, z-order |
| CREATE | `src/web-app/WebApp/Components/Canvas/WindowLayoutService.cs` | Persist to localStorage via JS interop |
| CREATE | `src/web-app/WebApp/Components/Designer/DesignerCanvas.razor` | Block graph editor (SVG-based) |
| CREATE | `src/web-app/WebApp/Components/Designer/BlockPalette.razor` | Categorized block type picker |
| CREATE | `src/web-app/WebApp/Components/Designer/PropertyEditor.razor` | Block parameter editor (typed inputs) |
| CREATE | `src/web-app/WebApp/Components/Designer/ConnectionRenderer.razor` | SVG bezier curves for block connections |
| CREATE | `src/web-app/WebApp/wwwroot/js/canvas-interop.js` | JS: pan/zoom, drag, HammerJS touch |

---

### MDI Layout Architecture

```
CanvasHost
└── WindowContainer (position: relative; overflow: hidden)
    └── DocumentWindow[] (position: absolute; z-index driven by WindowManager)
        ├── TitleBar (drag handle, minimize, maximize, close, detach)
        ├── ResizeHandles (8 directional)
        └── ContentSlot (renders any panel component as child)
```

- `WindowManager` is a scoped service tracking `ConcurrentDictionary<Guid, WindowState>`.
- Each `WindowState` is an immutable record (`with` expressions for mutations).
- `IWindowLayoutService` serialises the state collection to/from `localStorage` via `IJSRuntime`.
- `ResizeDirection` enum covers all 8 handles (N, NE, E, SE, S, SW, W, NW).
- Minimum window dimensions: 280 × 180 px.
- `OnLayoutChanged` event propagates state changes to `CanvasHost` for re-render.

---

### Designer Canvas Architecture

```
DesignerCanvas
├── SVG overlay (connections, drag ghost)
├── Block nodes (absolutely positioned div per block)
│   ├── Socket indicators (input left / output right)
│   └── Block body (type badge, parameter summary)
└── Block palette (FluentDrawer, slides in from left)
```

- Block nodes are `BlockViewModel` instances positioned via `left`/`top` CSS.
- Connections are `ConnectionViewModel` records rendered by `ConnectionRenderer` as SVG cubic bezier curves.
- Socket colours are determined by `BlockSocketType` enum from `MLS.Core.Designer`.
- Pan state: `_panX`, `_panY` (double); zoom: `_scale` (double, clamped 0.15–4.0).
- Drag ghost rendered as a translucent SVG rect during block-from-palette drag operations.
- `canvas-interop.js` handles pointer capture, HammerJS pinch-zoom, and middle-mouse pan.

---

### Skills Applied

- `.skills/premium-uiux-blazor.md`
- `.skills/web-apps.md`
- `.skills/designer.md`

---

### Acceptance Criteria

- [x] `DocumentWindow` can be dragged, resized, minimized, maximized, closed
- [x] Window positions/sizes persist to localStorage and restore on reload
- [x] `DesignerCanvas` can add blocks by dragging from palette
- [x] Socket connections drawn as smooth bezier curves with type-color coding
- [x] Pan (middle-mouse/touch) and zoom (wheel/pinch) work correctly
- [x] Touch gestures work on Android Chrome (HammerJS via JS interop)

**Session Status: ✅ COMPLETE**

---
