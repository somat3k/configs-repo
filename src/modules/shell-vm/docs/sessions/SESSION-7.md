# shell-vm — Session 7: REST Controller Pattern

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

---

## 7. REST Controller Pattern


```csharp
namespace MLS.ShellVM.Controllers;

/// <summary>HTTP API for shell session management. All endpoints return typed responses.</summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(
    ISessionManager _sessions,
    IExecutionEngine _engine
) : ControllerBase
{
    [HttpPost]        // POST /api/sessions
    [HttpDelete("{id:guid}")]
    [HttpGet]
    [HttpGet("{id:guid}")]
    [HttpPost("{id:guid}/exec")]
    [HttpPost("{id:guid}/resize")]
    [HttpGet("{id:guid}/output")]
}
```

---
