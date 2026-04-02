# broker — Session 4: Key Payload Types Used

> Use this document as context when generating Broker module code with GitHub Copilot.

---

## 4. Key Payload Types Used


| Direction | Type | Description |
|---|---|---|
| Receives | `ORDER_CREATE` | From Trader or DeFi — place new order |
| Receives | `ORDER_CANCEL` | Cancel order by clientOrderId |
| Sends | `ORDER_CONFIRMATION` | Order accepted by venue |
| Sends | `FILL_NOTIFICATION` | Order partially or fully filled |
| Sends | `POSITION_UPDATE` | Current position state |
| Sends | `MODULE_HEARTBEAT` | To Block Controller every 5 s |

---
