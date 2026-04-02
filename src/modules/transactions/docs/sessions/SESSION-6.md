# transactions — Session 6: Database Dependencies

> Use this document as context when generating Transactions module code with GitHub Copilot.

---

## 6. Database Dependencies


| Table | Purpose |
|---|---|
| `blockchain_addresses` | RPC endpoints, contract addresses |
| `transactions` | Full transaction lifecycle (EF Core entity) |
| `transaction_receipts` | Confirmed receipts with block data |

Redis is used for nonce tracking only (key: `nonce:{walletAddress}`).

---
