# Session Rules

These rules are enforced in every session. Copilot must apply them automatically.

---

## rule:hyperlink-enums
**All cross-module and cross-file references use fully-qualified enum paths.**

✅ `Module::Storage::Put`
❌ `"storage-put"` or `"put"`

All enums are registered in `.structure_pkg.json → enums[]`.

---

## rule:payload-envelope
**All inter-service messages are wrapped in a typed `Envelope`.**

```json
{ "type": "ENUM_VALUE", "version": 1, "session_id": "uuid", "payload": { ... } }
```

Never pass raw dicts/maps as messages. Always use a typed payload struct/class.

---

## rule:function-as-file
**One public function per file. All related helpers, types, and tests live in that same file.**

```
src/commands/create_user.rs    ← public fn create_user(...)
src/queries/get_user.rs        ← pub fn get_user(...)
src/invokers/auth_invoker.rs   ← pub fn invoke_auth(...)
```

---

## rule:single-file-module
**Each module compiles and runs as a standalone binary/service.**

- Exposes `GET /health` → `{"status":"ok"}`.
- Has its own `Dockerfile` and entry in `infra/docker-compose.yml`.
- Can be developed and tested independently.

---

## rule:session-complete
**A session is only complete when ALL of the following are true:**

- [ ] Module runs standalone: `make run MODULE=<name>`.
- [ ] Health check passes: `curl /health` → 200.
- [ ] Module registers on service mesh.
- [ ] All tests pass: `make test MODULE=<name>`.
- [ ] Session notes committed: `sessions/<session-id>/SESSION.md`.

---

## rule:no-secrets
**No credentials, tokens, or secrets committed to git.**

- Use `.env` (git-ignored) for local secrets.
- Use GitHub Secrets / environment variables in CI.
- The `.env.example` file documents required variables without values.

---

## rule:conventional-commits
**All commits follow Conventional Commits format.**

```
<type>(<scope>): <short description>
```

Types: `feat` | `fix` | `docs` | `refactor` | `test` | `chore` | `ci`
