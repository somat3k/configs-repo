## PR Checklist

### Code Quality
- [ ] Functions follow the **Function-as-File** rule
- [ ] All public APIs have doc-comments
- [ ] No magic strings — enums/constants used
- [ ] Payload types use the `Envelope` pattern
- [ ] No direct DB access outside `src/storage/`

### Module Completeness
- [ ] Module runs standalone (`make run MODULE=<name>`)
- [ ] `GET /health` returns `200 OK`
- [ ] Module registers on service mesh
- [ ] Tests pass (`make test-module MODULE=<name>`)

### Session
- [ ] Session notes committed to `sessions/<session-id>/SESSION.md`

### Security
- [ ] No secrets committed
- [ ] Input validated at API boundary
- [ ] SQL uses parameterised queries only

### Infrastructure
- [ ] Docker Compose updated if new service added
- [ ] `.env.example` updated if new env vars added

---

**Related issue:** Closes #
**Session ID:** `session-`
