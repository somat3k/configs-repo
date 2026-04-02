# shell-vm — Session 14: Compliance Checklist

> Use this document as the **primary context** when generating Shell VM module code with
> GitHub Copilot. Read every section before generating any file.

---

## 14. Compliance Checklist


Before marking a code change complete, verify all items:

- [ ] Module registers with Block Controller on startup via `POST /api/modules/register`
- [ ] Heartbeat sent every 5 seconds with metrics via `HeartbeatService`
- [ ] All WS messages use `EnvelopePayload` with `Version >= 1` and `Type` from `ShellVMMessageTypes`
- [ ] `ISessionManager` persists session state to Redis on every state transition
- [ ] `IAuditLogger` writes every command start and end to `shell_audit_log`
- [ ] `SessionWatchdog` reaps idle sessions beyond `MaxIdleSessionSeconds`
- [ ] PTY processes are killed when session terminates (no zombie processes)
- [ ] `ShellVMConfig.AllowedShells` is enforced — reject any shell not in the allow-list
- [ ] `CommandTimeoutSeconds` enforced — `CancellationToken` propagated to PTY process
- [ ] All HTTP and WS ports declared as `ShellVMNetworkConstants` constants — no magic numbers
- [ ] XML docs on every public interface, record, and method
- [ ] xUnit tests cover session create/exec/terminate state machine
- [ ] xUnit tests cover WebSocket output streaming
- [ ] Dockerfile uses multi-stage build (see `.skills/multi-stage-dockerfile.md`)
- [ ] Docker service registered in `docker-compose.yml` with correct ports `5950:5950` / `6950:6950`
