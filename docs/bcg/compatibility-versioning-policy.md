# Compatibility and Versioning Policy
## BCG Session 04 — Schema Evolution Rules and Migration Law

**Status**: Authoritative  
**Version**: 1.0  
**Depends on**: transport-governance-spec.md, protobuf-package-map.md, envelope-law.md

---

## 1. Purpose

This document defines the three-version model, the three compatibility change categories, the schema promotion policy, and the migration process for every message schema in the BCG ecosystem.

---

## 2. Three-Version Model

The BCG platform tracks three versions independently for every message path:

| Version Axis | Tracks | Moves When |
|-------------|--------|-----------|
| **Envelope version** | `EnvelopePayload.Version` / `EnvelopeV2.Version` | Envelope contract fields change |
| **Payload schema version** | `EnvelopeV2.PayloadSchema` (e.g. `"bcg.module.RegisterModule:2"`) | Business payload fields change |
| **Module implementation version** | Module's internal application version | Module logic evolves |

These three versions may move at different rates. A module may release implementation v3 while its payload schema remains at v1 and the envelope version remains at v1.

---

## 3. Compatibility Change Categories

### 3.1 Backward-Compatible Change

A change is backward-compatible if **older consumers can process newer payloads** without modification.

Examples:
- adding a new optional protobuf field
- adding a new tolerable enum value (with `UNSPECIFIED = 0` safety)
- adding new optional fields to `EnvelopeV2`
- adding a new message type that existing consumers can ignore

**Governance**: no migration note required. Schema version incremented as a minor step. Owning module maintainer approves.

### 3.2 Forward-Compatible Tolerable Change

A change is forward-compatible if **newer consumers can process older payloads** without modification.

Examples:
- consumer ignoring unknown optional fields from older senders
- topic listeners accepting event types outside their declared interest set
- `EnvelopeValidator` tolerating absence of optional extended fields

**Governance**: no migration note required. Consumers must be built to tolerate unknown fields by default (protobuf unknown field handling; `System.Text.Json` `JsonExtensionData` where applicable).

### 3.3 Breaking Change

A change is breaking if it **requires all consumers to update** before the producer deploys.

Examples:
- changing the semantic meaning of an existing field
- removing a field without a reservation entry
- changing an enum value's meaning (not just adding a new value)
- renaming a proto field in a way that changes the wire number
- making a previously optional field required with no fallback

**Governance** (mandatory steps for every breaking change):
1. Declare the change as breaking in the PR description.
2. Increment the major schema version.
3. Write a migration note using the `migration-note-template.md`.
4. Provide a compatibility shim or dual-read adapter for the transition period.
5. Declare a rollback path and the minimum window before old schema is retired.
6. Architecture review approval required before merge.

---

## 4. Version String Format

Payload schema version strings follow this format:

```
<proto_package>.<MessageName>:<version>
```

Examples:
- `bcg.module.RegisterModule:1`
- `bcg.tensor.BcgTensorProto:2`
- `bcg.session.SessionJoinRequest:1`

The `EnvelopeV2.PayloadSchema` field carries this string. Validators must parse and compare this string against the consumer's declared supported versions.

---

## 5. Schema Promotion Policy

A schema cannot be promoted to production-grade unless it has passed all of the following gates:

| Gate | Requirement |
|------|-------------|
| Lint | `buf lint` or protoc equivalent passes with zero warnings |
| Semantic review | Owning module maintainer approves field meanings |
| Contract tests | Round-trip serialization tests pass for all messages |
| Compatibility notes | Change category declared (backward / forward / breaking) |
| Failure modes | All rejection and error paths described |
| Sample payloads | At least one representative sample per message type |

Schemas in `Reserved` status in the package map must not have contract tests required. They may have draft proto files only.

---

## 6. Envelope Version Policy

The `EnvelopePayload` base record is frozen at its current field set. It may not change.

The `EnvelopeV2` record extends the base. New optional fields may be added as backward-compatible changes. Any field in `EnvelopeV2` that becomes mandatory for routing must go through a breaking change review, since mandating previously-optional fields changes consumer behavior.

The envelope version field (`Version`) tracks the envelope contract version, not the payload schema version. The envelope version must be incremented when any envelope field changes semantics (breaking) or when a new mandatory envelope field is added (breaking). Adding optional envelope fields does not require an envelope version increment.

---

## 7. Dual-Read Compatibility Window

When a breaking change is deployed:

1. The new producer must support emitting both old and new schema during the transition window.
2. The transition window must be declared in the migration note (minimum: one full deployment cycle).
3. Consumers must declare their supported schema versions in their module capability registration.
4. The Block Controller may use declared capability versions to route to compatible consumers during the window.
5. After the transition window closes, the old schema version may be retired.

---

## 8. Reserved Field Policy (Protobuf)

When a protobuf field is removed:

1. Its field number must be added to the `reserved` list in the proto file.
2. Its field name must be added to the `reserved` names list.
3. A comment must record the version in which it was removed and the reason.

Example:
```proto
message RegisterModule {
  reserved 7;
  reserved "legacy_tag";
  // Field 7 (legacy_tag) removed in schema v2 — replaced by capabilities repeated field.
  ...
}
```

Failure to reserve deleted field numbers is a blocking review finding.
