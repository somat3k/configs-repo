# Block Controller — Session Prompt

> Use this document as context when generating Block Controller code with GitHub Copilot.

---

## Module Identity

| Field | Value |
|-------|-------|
| Name | `block-controller` |
| Role | Central orchestration hub, root module |
| Namespace | `MLS.BlockController` |
| HTTP Port | `5100` |
| WebSocket Port | `6100` |
| Container | `mls-block-controller` |
| Hub URL | `ws://block-controller:6100/hubs/block-controller` |

## Standalone Operation

The Block Controller is designed to run as a fully independent network node. It requires
no other modules to start, accept connections, or route envelopes. All connections join
the `broadcast` group automatically; peers with a known ID join their own group for
targeted delivery. See `sessions/SESSION-3.md` for the full protocol.

---

## Sessions

- [SESSION-1.md — Module Identity](sessions/SESSION-1.md)
- [SESSION-2.md — Required Interfaces](sessions/SESSION-2.md)
- [SESSION-3.md — Standalone Network Node + Bidirectional Protocol](sessions/SESSION-3.md)
