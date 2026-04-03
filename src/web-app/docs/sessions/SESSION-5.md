# web-app — Session 5: Module Observatory

> Use this document as context when generating Web App module code with GitHub Copilot.

---

## 5. Module Observatory


The Dashboard page aggregates health from all registered modules via Block Controller:

```csharp
// ModuleCard displays:
// - Module name, status (Healthy / Degraded / Offline)
// - Uptime seconds
// - CPU %, memory MB
// - Last heartbeat timestamp (relative: "2s ago")
// - Module-specific metrics (active sessions, orders processed, etc.)
```

---
