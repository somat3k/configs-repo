---
mode: agent
description: "BCG Session 14 — Security, Identity, and Trust Fabric"
status: "⏳ Pending — no formal identity, RBAC, or artifact trust layer exists"
depends-on: ["session-04", "session-12", "session-13"]
produces: ["docs/bcg/session-14-*.md", "src/core/MLS.Core/Security/", "src/block-controller/"]
---

# Session 14 — Security, Identity, and Trust Fabric

> **Status**: ⏳ Pending — modules currently join the fabric without any cryptographic identity or authorization checks.

## Session Goal

Introduce production-grade trust boundaries: every module must prove identity on registration, privileged commands require authorization, and model artifacts must carry a verifiable trust attestation.

## Todo Checklist

### Governance Documents (`docs/bcg/`)
- [ ] `session-14-extended-document.md` (source: `.prompts-update/BCG_Session_14_Extended_Document.md`)
- [ ] `security-architecture.md` — threat model, trust boundaries, attack surface inventory
- [ ] `trust-identity-model.md` — module identity: self-signed JWT on first registration, BC-signed token after
- [ ] `rbac-matrix.md` — roles: Operator, Module, Admin, ReadOnly; permission table per API and hub method
- [ ] `secrets-certificate-policy.md` — secrets rotation, certificate lifetime, Vault/env-var strategy
- [ ] `audit-logging-rules.md` — which actions must be audited, log format, tamper-evident requirements

### C# Security Abstractions (`src/core/MLS.Core/Security/`)
- [ ] `IModuleIdentityVerifier.cs` — `VerifyAsync(moduleId, token): Task<VerificationResult>`
- [ ] `VerificationResult.cs` — record: isValid, role, claims, expiresAt
- [ ] `ModuleRole.cs` — enum: Module, Operator, Admin, ReadOnly
- [ ] `IPermissionPolicy.cs` — `IsAllowed(ModuleRole, string action): bool`
- [ ] `JwtModuleTokenService.cs` — issues and validates short-lived JWT for module identity
- [ ] `ArtifactTrustAttestation.cs` — record: artifactId, issuerModuleId, signature, algorithm, issuedAt
- [ ] `IArtifactTrustVerifier.cs` — `VerifyAttestation(ArtifactTrustAttestation): bool`
- [ ] `AuditLogEntry.cs` — record: timestamp, actorId, actorRole, action, resourceId, outcome, traceId
- [ ] `IAuditLogger.cs` — `LogAsync(AuditLogEntry): Task`
- [ ] Add `IDENTITY_VERIFIED`, `IDENTITY_REJECTED`, `PRIVILEGE_ESCALATION_DENIED`, `ARTIFACT_TRUST_FAILED` to `MessageTypes`

### Block Controller: Identity Gate (`src/block-controller/`)
- [ ] Add auth middleware: validate JWT on hub `OnConnectedAsync` — disconnect anonymous connections
- [ ] Extend `ModulesController.Register` to require identity token; store verified role
- [ ] Add `POST /api/admin/issue-token` — Admin-only endpoint to issue module identity tokens
- [ ] Add `RouteAdmission` permission check: Admin routes not accessible to Module role
- [ ] Add `IAuditLogger` to log registration, drain, promotion, and admin actions

### ShellVM: Command Authorization
- [ ] Add `IPermissionPolicy` check in `PtyProviderService.SpawnAsync` — allow-list + role guard
- [ ] Emit `PRIVILEGE_ESCALATION_DENIED` if command outside allowed set for role

### Infrastructure
- [ ] Add `audit_log` Postgres table migration: `(id UUID, timestamp TIMESTAMPTZ, actor_id TEXT, actor_role TEXT, action TEXT, resource_id TEXT, outcome TEXT, trace_id TEXT)`

### Tests
- [ ] `JwtModuleTokenServiceTests.cs` — issue, validate, expiry, tampered-token rejection
- [ ] `PermissionPolicyTests.cs` — Admin allowed, Module denied for admin actions
- [ ] `ArtifactTrustVerifierTests.cs` — valid signature passes, tampered fails
- [ ] `AuditLoggerTests.cs` — log written on registration and drain

## Skills to Apply

```
.skills/dotnet-devs.md               — JWT, middleware, IOptions<T>, primary constructors
.skills/system-architect.md          — trust model, RBAC, zero-trust principles
.skills/websockets-inferences.md     — hub auth middleware, OnConnectedAsync
.skills/storage-data-management.md   — audit log in Postgres
```

## Copilot Rules to Enforce

- `.github/copilot-rules/rule-payload-envelope.md` — security events via typed EnvelopePayload
- Modules CANNOT join fabric anonymously after Session 14 — token required
- Privileged actions (drain, promote, admin) MUST be audit-logged
- Secrets MUST NOT be committed to source code — use environment variables or Vault references

## Acceptance Gates

- [ ] Anonymous hub connection is rejected with 401
- [ ] Module with valid JWT joins fabric and heartbeats successfully
- [ ] Admin-only `POST /api/admin/issue-token` rejected with 403 for Module role
- [ ] `audit_log` row written for every registration and drain
- [ ] All new tests pass: `dotnet test`
- [ ] 5 governance documents committed to `docs/bcg/`

## Key Source Paths

| Path | Purpose |
|------|---------|
| `src/core/MLS.Core/Security/` | Create security abstractions here |
| `src/block-controller/MLS.BlockController/Hubs/BlockControllerHub.cs` | Add auth middleware |
| `src/block-controller/MLS.BlockController/Controllers/ModulesController.cs` | Add token validation |
| `src/modules/shell-vm/MLS.ShellVM/Services/PtyProviderService.cs` | Add permission check |
| `infra/postgres/init/` | audit_log migration |
| `.prompts-update/BCG_Session_14_Extended_Document.md` | Full session spec |
