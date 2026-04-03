# shell-vm — Session 11: Message Flow Diagrams

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

---

## 11. Message Flow Diagrams


### 11.1 Module-Triggered Script Execution

```
ML Runtime → BC (ROUTE_MESSAGE: SHELL_EXEC_REQUEST)
           → BC routes → ShellVM HTTP POST /api/sessions/{id}/exec
           ← ShellVM responds with CommandExecution (202 Accepted)
           
ShellVM → Channel<OutputChunk> (internal fan-out)
       → SignalR group: "session:{id}"
       → Web App ← receives SHELL_OUTPUT in real-time

ShellVM → BC (SHELL_SESSION_STATE: Completed, exit_code: 0)
```

### 11.2 Interactive Console (Operator via Web App)

```
Web App → ShellVM WS (SHELL_EXEC_REQUEST: session_id, cmd="/bin/bash")
ShellVM creates ExecutionBlock → state: Starting → Running
ShellVM → Web App (SHELL_SESSION_CREATED)

Operator types → Web App → ShellVM WS (SHELL_INPUT: data="ls -la\n")
ShellVM PTY stdin ← data
PTY stdout → ShellVM OutputBroadcaster → Web App (SHELL_OUTPUT chunks)

Operator exits → Web App → ShellVM WS (terminate)
ShellVM → Web App (SHELL_SESSION_STATE: Terminated)
ShellVM → BC (MODULE_HEARTBEAT: active_sessions -1)
```

### 11.3 Block Controller Heartbeat

```
HeartbeatService (every 5s):
  ShellVM → BC HTTP POST /api/modules/{id}/heartbeat
  payload: {
    status: "healthy",
    uptime_seconds: 3600,
    metrics: { active_sessions: 3, commands_executed: 142, cpu_percent: 18.4, memory_mb: 312 }
  }
```

---
