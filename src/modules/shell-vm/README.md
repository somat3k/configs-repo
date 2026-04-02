# Shell VM Module — Sandboxed Shell Execution & Execution Console

> **Status**: 🔧 Scaffold — ready for implementation.

## Overview

The `shell-vm` module provides **sandboxed shell execution**, **PTY-based terminal sessions**, and
an **operator execution console** for the MLS platform. It is inspired by the
[WaveTerm](https://github.com/wavetermdev/waveterm) block-based terminal architecture and adapts
the concept to the MLS distributed network: every shell session is a first-class "execution block"
with a unique ID, persistent state, streamed I/O, and full Block Controller registration.

Modules can delegate script execution (Python ML training, `ape` smart-contract operations,
diagnostic commands) to shell-vm without spawning raw processes themselves. Operators interact
with a live multi-session console via the Web App.

---

## Responsibilities

| Responsibility | Detail |
|---|---|
| PTY session management | Create, resize, pause, resume, and destroy pseudo-terminal sessions |
| Script execution engine | Execute Python, shell, and `ape` scripts in isolated environments |
| Execution block tracking | Each execution is a typed `ExecutionBlock` with state machine |
| Output streaming | Stream `stdout`/`stderr` in real-time via WebSocket to subscribers |
| Session persistence | Sessions survive WebSocket reconnections using a Redis-backed registry |
| Audit logging | All commands and outputs written to PostgreSQL `shell_audit_log` |
| Block Controller integration | Registers on startup, sends heartbeats every 5 s, receives routed commands |

---

## Ports

| Protocol | Port |
|----------|------|
| HTTP API | 5950 |
| WebSocket | 6950 |

---

## Execution Block State Machine

```
Created → Starting → Running → Paused → Completed
                                │               │
                              Error ←──────────┘
                                │
                             Terminated
```

Each `ExecutionBlock` transitions through well-defined states. The current state is always
broadcast to all WebSocket subscribers as a `SHELL_SESSION_STATE` envelope.

---

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/sessions` | Create a new shell session |
| `DELETE` | `/api/sessions/{id}` | Terminate a session |
| `GET` | `/api/sessions` | List all active sessions |
| `GET` | `/api/sessions/{id}` | Get session details and current state |
| `POST` | `/api/sessions/{id}/exec` | Execute a command in a session |
| `POST` | `/api/sessions/{id}/resize` | Resize the PTY (cols × rows) |
| `GET` | `/api/sessions/{id}/output` | Get buffered output (last N lines) |
| `GET` | `/api/sessions/{id}/audit` | Get audit log for session |
| `GET` | `/health` | Module health check |

---

## WebSocket Events

All WebSocket messages use the [Envelope Protocol](../../../docs/payload-schemas.md).

| Type | Direction | Description |
|------|-----------|-------------|
| `SHELL_EXEC_REQUEST` | ← Client | Request command execution in a session |
| `SHELL_INPUT` | ← Client | Send raw stdin bytes to a PTY session |
| `SHELL_RESIZE` | ← Client | Resize PTY dimensions |
| `SHELL_OUTPUT` | → Client | Stream stdout/stderr chunk |
| `SHELL_SESSION_STATE` | → Client | Session state change notification |
| `SHELL_SESSION_CREATED` | → All | Broadcast new session creation |
| `SHELL_SESSION_TERMINATED` | → All | Broadcast session termination |
| `MODULE_HEARTBEAT` | → BC | Block Controller heartbeat |

---

## Architecture

```
src/modules/shell-vm/
└── ShellVM/
    ├── ShellVM.csproj
    ├── Program.cs                          # ASP.NET Core + SignalR setup
    ├── appsettings.json
    ├── Dockerfile
    ├── Constants/
    │   └── ShellVMConstants.cs             # Ports, message types, limits
    ├── Models/
    │   ├── ExecutionBlock.cs               # PTY session entity (EF Core)
    │   ├── ShellSession.cs                 # In-memory session state
    │   ├── AuditEntry.cs                   # Audit log record
    │   └── ShellVMConfig.cs                # Strongly-typed configuration
    ├── Payloads/
    │   ├── ShellExecRequestPayload.cs      # SHELL_EXEC_REQUEST payload
    │   ├── ShellInputPayload.cs            # SHELL_INPUT payload
    │   ├── ShellResizePayload.cs           # SHELL_RESIZE payload
    │   ├── ShellOutputPayload.cs           # SHELL_OUTPUT payload
    │   └── ShellSessionStatePayload.cs     # SHELL_SESSION_STATE payload
    ├── Interfaces/
    │   ├── ISessionManager.cs              # Session lifecycle contract
    │   ├── IPtyProvider.cs                 # PTY abstraction (xterm.js-style)
    │   ├── IExecutionEngine.cs             # Script/command execution
    │   ├── IAuditLogger.cs                 # Structured audit contract
    │   └── IOutputBroadcaster.cs           # Real-time output streaming
    ├── Services/
    │   ├── SessionManager.cs               # Session create/destroy/persist
    │   ├── PtyProviderService.cs           # System.Diagnostics.Process + PTY
    │   ├── ExecutionEngine.cs              # Command dispatch and isolation
    │   ├── AuditLogger.cs                  # PostgreSQL audit writer
    │   └── OutputBroadcaster.cs            # Channel<T> → SignalR fan-out
    ├── Hubs/
    │   └── ShellVMHub.cs                   # SignalR hub — WS entry point
    ├── Controllers/
    │   └── SessionsController.cs           # HTTP REST API
    ├── Background/
    │   ├── HeartbeatService.cs             # 5-second BC heartbeat
    │   └── SessionWatchdog.cs              # Reap timed-out sessions
    └── Migrations/                         # EF Core migrations
```

---

## Key Payload Types

> `Envelope.session_id` carries the shell session identifier for all `SHELL_*` messages — it is not repeated inside the payload body.

### `SHELL_EXEC_REQUEST`
```json
{
  "command": "python train.py --model model_t",
  "working_dir": "/app/scripts",
  "env": { "PYTHONPATH": "/app" },
  "timeout_seconds": 600,
  "capture_output": true
}
```

### `SHELL_OUTPUT`
```json
{
  "stream": "stdout",
  "chunk": "Epoch 1/50 — loss: 0.421\n",
  "sequence": 42,
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### `SHELL_SESSION_STATE`
```json
{
  "previous_state": "Running",
  "current_state": "Completed",
  "exit_code": 0,
  "duration_ms": 4821
}
```

---

## Dependencies

| Service | Purpose |
|---------|---------|
| `block-controller` | Registration, heartbeat, routed command reception |
| `redis` | Session registry persistence, output ring-buffer |
| `postgres` | `execution_blocks` table, `shell_audit_log` table |

---

## Session prompt: [docs/SESSION.md](docs/SESSION.md)
