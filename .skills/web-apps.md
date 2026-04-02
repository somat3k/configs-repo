---
name: web-apps
source: github/awesome-copilot/skills/web-coder
description: 'Web application development for the MLS platform using ASP.NET Core, Blazor, SignalR, and RESTful APIs.'
---

# Web Application Development — MLS Trading Platform

## Project Context
The web application module (`src/web-app/`) is the primary user interface for the MLS platform. It is built with **ASP.NET Core Blazor** (interactive server-side rendering with WebAssembly fallback), **Microsoft FluentUI Blazor** components, and connects to all backend modules via SignalR WebSockets and HTTP APIs.

## Architecture
- **Blazor Interactive Server** for real-time trading dashboards
- **Blazor WebAssembly** fallback for offline-capable pages
- **SignalR Hub** connections to each module's WebSocket server
- **MDI Canvas** layout supporting multiple detachable windows/panels per module
- **NuxtJS** static GitHub Pages documentation site (separate from the Blazor app)

## Component Structure
- `Pages/` — top-level routable pages (Dashboard, Trader, Arbitrager, DeFi, Settings)
- `Components/` — reusable UI components (Charts, DataGrid, Panels, StatusBar)
- `Layout/` — MDI canvas layout, navigation, and shell
- `Services/` — client-side services (SignalR clients, HTTP clients, state management)
- `Models/` — view models and DTOs for UI binding

## Real-Time Data Patterns
- Use `HubConnection` from `Microsoft.AspNetCore.SignalR.Client` for WebSocket connections
- Implement reconnection policies with exponential backoff
- Use `IAsyncEnumerable<T>` streaming for market data feeds
- Bind streaming data to charts using Blazor state management

## Routing Conventions
- `/` — Main dashboard (multi-module overview)
- `/trader` — Trader algo-model interface
- `/arbitrager` — Arbitrager algo-model interface
- `/defi` — DeFi services interface
- `/network` — Network topology and module status
- `/observatory` — Metrics, performance, plots, manifests
- `/settings` — Module configuration and preferences

## Performance
- Use `@key` directive for efficient list rendering
- Implement virtual scrolling for large data grids
- Lazy load module-specific pages with `@attribute [StreamRendering]`
- Cache static module data with `IMemoryCache`
