---
name: premium-uiux-blazor
source: github/awesome-copilot/skills/premium-frontend-ui + fluentui-blazor
description: 'Premium UI/UX craftsmanship for Blazor applications using Microsoft FluentUI — immersive trading dashboards, MDI canvas, real-time charts, and high-performance data visualization.'
---

# Premium UI/UX for Blazor — MLS Trading Platform

## Creative Foundation
The MLS platform UI follows **Cyber/Technical** aesthetic principles:
- Dark mode dominance with deep navy/charcoal backgrounds
- Glowing accent colors: cyan for live data, amber for warnings, green for profits, red for losses
- Monospaced typography for data display (JetBrains Mono, Fira Code)
- Rapid, staggered reveal animations for data panel entrances
- Real-time sparklines and candlestick charts embedded in data grids

## FluentUI Blazor Critical Rules
- Always register `builder.Services.AddFluentUIComponents()` in Program.cs
- Use `ServiceLifetime.Scoped` for Blazor Server (default)
- Add providers to MainLayout.razor: `<FluentToastProvider />`, `<FluentDialogProvider />`, `<FluentMessageBarProvider />`, `<FluentTooltipProvider />`
- Use strongly-typed icon references: `Icons.Regular.Size24.ChartMultiple`
- Apply `<FluentDesignTheme Mode="DesignThemeModes.Dark" />` in the root layout

## MDI Canvas Layout
- Implement a Multi-Document Interface canvas as the main shell
- Each module panel is a floating, resizable, dockable window
- Use CSS Grid + `position: absolute` for MDI panel management
- Support panel minimize, maximize, close, and detach operations
- Persist panel layout to localStorage via JS interop

## Chart Components
- Use **Blazor Chart.js** or **ApexCharts.Blazor** for candlestick and OHLCV charts
- Implement real-time updates via SignalR without full component re-renders
- Support time-frame switching: 1m, 5m, 15m, 1h, 4h, 1D
- Include volume bars, RSI, MACD, Bollinger Bands as overlay indicators

## Typography Engine
- Headlines: `clamp(1.5rem, 3vw, 3rem)` fluid sizing
- Data values: `font-family: 'JetBrains Mono', monospace` with tabular numerals
- Status indicators: color-coded badges with animated pulse for live state

## Motion Design
- Panel entrance: `transform: translateY(20px) → translateY(0)` with `opacity: 0 → 1`
- Data update flash: brief background highlight flash on value change
- Loading states: skeleton placeholders matching content dimensions
- Wrap all animations in `@media (prefers-reduced-motion: no-preference)`

## Performance Imperative
- Only animate `transform` and `opacity` properties
- Use `will-change: transform` on moving panels, remove post-animation
- Apply `@key` directive on all data-bound lists
- Implement `IntersectionObserver` via JS interop for lazy-loading off-screen panels
