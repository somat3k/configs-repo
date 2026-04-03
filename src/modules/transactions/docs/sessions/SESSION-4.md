# transactions — Session 4: Key Payload Types Used

> Use this document as context when generating Transactions module code with GitHub Copilot.

---

## 4. Key Payload Types Used


| Direction | Type | Description |
|---|---|---|
| Receives | `TX_CREATE` | From Broker or DeFi — create and submit transaction |
| Receives | `TX_CANCEL` | Cancel pending transaction (if not yet mined) |
| Sends | `TX_SUBMITTED` | Transaction hash confirmed by RPC node |
| Sends | `TX_CONFIRMED` | Transaction mined with receipt |
| Sends | `TX_FAILED` | Transaction reverted or rejected |
| Sends | `MODULE_HEARTBEAT` | To Block Controller every 5 s |

---
