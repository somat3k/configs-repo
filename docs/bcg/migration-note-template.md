# Migration Note Template
## BCG Session 04 — Breaking Schema Change Documentation

**Status**: Authoritative Template  
**Version**: 1.0  
**Depends on**: compatibility-versioning-policy.md

---

## Purpose

This template must be completed for every breaking schema change before the change may be merged or promoted to production. Copy this file and fill in all fields. Store the completed note alongside the schema PR.

---

## Migration Note

### Header

| Field | Value |
|-------|-------|
| **Schema / Package** | (e.g. `bcg.module.RegisterModule`) |
| **Old schema version** | (e.g. `1`) |
| **New schema version** | (e.g. `2`) |
| **Change category** | Breaking |
| **Author** | (GitHub username) |
| **Date** | (ISO 8601 date) |
| **PR / Commit** | (link or SHA) |
| **Architecture review** | (reviewer name + approval date) |

---

### 1. Nature of the Breaking Change

Describe precisely what changed and why the change is breaking. Include:

- which field(s) or enum value(s) changed
- the old semantic meaning
- the new semantic meaning
- why a backward-compatible alternative was not chosen

---

### 2. Affected Consumers

List every module or system that consumes this schema and must update:

| Module | Current version supported | Must update to | Update deadline |
|--------|--------------------------|---------------|----------------|
| (module name) | v1 | v2 | (date) |

---

### 3. Transition Window

| Field | Value |
|-------|-------|
| **Transition window opens** | (date — when new schema is deployed to staging) |
| **Transition window closes** | (date — when old schema is retired) |
| **Minimum window length** | (at least one full deployment cycle) |

During the transition window:
- The producer must emit both old and new schema forms simultaneously (dual-emit).
- Consumers must declare which schema version they support in capability registration.
- The Block Controller will route to the compatible consumer version during the window.

---

### 4. Compatibility Shim / Dual-Read Adapter

Describe any compatibility shim provided:

- location of the shim code
- which consumers can use the shim
- when the shim will be removed

If no shim is required, explain why.

---

### 5. Reserved Fields

List any protobuf field numbers or names being reserved as part of this change:

```proto
reserved <field_number>;
reserved "<field_name>";
// Removed in schema v<N> — <reason>.
```

---

### 6. Rollback Plan

Describe the rollback plan if the new schema causes production issues after the transition window opens:

- what configuration change rolls back the producer to the old schema
- what state cleanup is required on rollback
- whether the old schema version can be re-activated without data loss

---

### 7. Sample Payloads

Provide at least one representative sample payload for:

**Old schema (v_old)**:
```json
{
  "example": "fill in"
}
```

**New schema (v_new)**:
```json
{
  "example": "fill in"
}
```

---

### 8. Test Coverage

List the tests that verify the new schema and confirm backward compatibility during the transition window:

| Test | Location | What it verifies |
|------|----------|-----------------|
| Round-trip serialization | (path) | New schema serializes and deserializes correctly |
| Version tolerance | (path) | Old consumers ignore new optional fields |
| Dual-emit | (path) | Producer emits both schema versions during transition |
| Rollback | (path) | Old schema version still accepted after rollback |

---

### 9. Sign-Off Checklist

- [ ] Nature of breaking change described
- [ ] All affected consumers listed with update deadlines
- [ ] Transition window declared with specific dates
- [ ] Dual-emit or shim provided
- [ ] Reserved fields added to proto file
- [ ] Rollback plan documented
- [ ] Sample payloads provided
- [ ] Tests listed and passing
- [ ] Architecture review completed
