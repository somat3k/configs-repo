# shell-vm — Session 2: Architecture Source — WaveTerm Concepts Applied

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

---

## 2. Architecture Source — WaveTerm Concepts Applied


This module is architecturally inspired by [WaveTerm](https://github.com/wavetermdev/waveterm):

| WaveTerm Concept | MLS Shell VM Equivalent |
|---|---|
| **Block** — independent terminal widget with unique ID | `ExecutionBlock` — PTY session or command run with UUID |
| **Durable SSH session** — survives reconnects | `ISessionManager` + Redis registry persists sessions across WS drops |
| **WSH protocol** — shell-to-shell data sharing | `SHELL_EXEC_REQUEST` envelope — any module can trigger execution |
| **Command Blocks** — isolated individual command tracking | `CommandExecution` — each command gets its own audit entry |
| **PTY streaming** — real-time stdout/stderr | `IOutputBroadcaster` — `Channel<OutputChunk>` fan-out via SignalR |
| **Block state machine** | `ExecutionBlockState` enum with transitions |

---
