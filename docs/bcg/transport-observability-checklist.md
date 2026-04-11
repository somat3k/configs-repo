# Transport Observability Checklist
## BCG Session 04 — Required Metrics and Traces Per Transport Class

**Status**: Authoritative  
**Version**: 1.0  
**Depends on**: transport-governance-spec.md, envelope-law.md

---

## 1. Purpose

This document specifies the minimum telemetry that every transport class must emit for a BCG message path to be observable. Every tensor-critical or route-critical action must produce enough telemetry to reconstruct the full communication audit trail.

---

## 2. Universal Telemetry Fields (All Classes)

Every transport event record must include:

| Field | Source |
|-------|--------|
| `trace_id` | `EnvelopeV2.TraceId` |
| `correlation_id` | `EnvelopeV2.CorrelationId` |
| `causation_id` | `EnvelopeV2.CausationId` |
| `message_type` | `EnvelopeV2.Type` |
| `envelope_version` | `EnvelopeV2.Version` |
| `payload_schema` | `EnvelopeV2.PayloadSchema` |
| `transport_class` | `EnvelopeV2.TransportClass` |
| `routing_scope` | `EnvelopeV2.RoutingScope` |
| `sender_module_id` | `EnvelopeV2.ModuleId` |
| `target_module_id` | `EnvelopeV2.TargetModule` (if applicable) |
| `topic` | `EnvelopeV2.Topic` (if applicable) |
| `timestamp_sent` | `EnvelopeV2.Timestamp` |
| `timestamp_received` | Set at ingress admission |
| `timestamp_processed` | Set at route completion |
| `validation_outcome` | Valid / Rejected / Quarantined |
| `transport_error_class` | null or error type constant |
| `payload_size_bytes` | Byte count of serialized payload body |
| `priority` | `EnvelopeV2.Priority` |

---

## 3. Class A — Authoritative Machine Contract Telemetry

In addition to universal fields:

| Field | Description |
|-------|-------------|
| `route_outcome` | Routed / Rejected / Queued / Timeout |
| `schema_version_match` | true / false |
| `target_module_health_state` | Health state of target at route time |
| `dedupe_status` | Fresh / Duplicate / DedupeWindowExpired |
| `retry_attempt` | 0 for initial; N for Nth retry |
| `execution_duration_ms` | Duration of execution step if applicable |
| `latency_p50_ms` | Running latency bucket (updated by controller) |
| `latency_p95_ms` | Running latency bucket |
| `latency_p99_ms` | Running latency bucket |
| `block_id` | If block-scoped |
| `task_id` | If task-scoped |

---

## 4. Class B — Live Event and Stream Fabric Telemetry

In addition to universal fields:

**Per connection**:
| Field | Description |
|-------|-------------|
| `connection_id` | SignalR/WebSocket connection identifier |
| `connection_start` | When connection was established |
| `connection_end` | When connection closed (null if active) |
| `stream_type` | Subscription type (topic, session, broadcast) |

**Per event**:
| Field | Description |
|-------|-------------|
| `event_sequence` | Monotonic sequence number per connection |
| `backpressure_state` | None / Throttled / Dropped |
| `dropped_events_count` | Count of dropped events in current window |
| `idle_time_ms` | Time since last event on this stream |
| `subscriber_count` | Number of active subscribers at emit time |
| `resume_attempt` | Whether this is a resume after disconnect |

---

## 5. Class C — External Trigger and Compatibility Ingress Telemetry

In addition to universal fields:

| Field | Description |
|-------|-------------|
| `external_source` | Caller identity or IP bucket |
| `intake_validation_outcome` | Valid / SchemaError / AuthError / NormalizedViaDataEvolution |
| `data_evolution_invoked` | true / false |
| `http_status_code` | HTTP response code returned to caller |
| `normalization_duration_ms` | Time spent in DataEvolution normalization (if invoked) |

---

## 6. Class D — Artifact Reference Telemetry

In addition to universal fields:

| Field | Description |
|-------|-------------|
| `artifact_ref` | Reference string (Redis key / Postgres ID / IPFS CID) |
| `artifact_storage_mode` | Redis / Postgres / Ipfs |
| `artifact_size_bytes` | Size of the referenced artifact body |
| `integrity_check_outcome` | Passed / Failed / Skipped |
| `retrieval_duration_ms` | Time to retrieve artifact body if consumer fetched inline |
| `retrieval_authorized` | true / false |
| `lineage_tensor_id` | Tensor ID this artifact is linked to (if applicable) |

---

## 7. Required Metric Aggregations

The Block Controller and each module must maintain the following aggregated metrics:

| Metric | Aggregation | Retention |
|--------|-------------|-----------|
| Route success rate | Per message type, per minute | 7 days |
| Route failure rate | Per message type, per error class, per minute | 7 days |
| Admission rejection rate | Per transport class, per minute | 7 days |
| Validation error rate | Per error type, per minute | 7 days |
| Latency p50/p95/p99 | Per message type, per 5-minute bucket | 30 days |
| Payload size distribution | Per transport class, per hour | 30 days |
| Schema mismatch count | Per (sender, receiver, schema) tuple, per hour | 30 days |
| Retry rate | Per message type, per hour | 30 days |
| Stream idle timeout rate | Per stream type, per hour | 7 days |
| Artifact retrieval failure rate | Per storage mode, per hour | 7 days |

---

## 8. Streaming Lane Observability

Streaming Class B connections must emit lifecycle events:

| Event | When Emitted |
|-------|-------------|
| `STREAM_CONNECTED` | When a new WebSocket/SignalR connection is established |
| `STREAM_DISCONNECTED` | When a connection closes (reason included) |
| `STREAM_IDLE_TIMEOUT` | When idle threshold is exceeded before close |
| `STREAM_BACKPRESSURE_EVENT` | When rate limit is exceeded and events are being dropped |
| `STREAM_RESUMED` | When a subscriber reconnects and stream resumes |

---

## 9. Certification Requirement

A transport path is not observable-certified unless:

1. All universal telemetry fields are emitted for every message.
2. Class-specific fields are emitted for the correct transport class.
3. Metric aggregations are populated at the declared cadence.
4. Streaming events are emitted for all connection lifecycle transitions.
5. Telemetry is validated by a test that asserts the presence of mandatory fields in emitted records.
