# transactions — Session 2: Critical Rules

> Use this document as context when generating Transactions module code with GitHub Copilot.

---

## 2. Critical Rules


1. All contract addresses and RPC endpoints loaded from PostgreSQL `blockchain_addresses` — **never hardcoded**
2. Transaction signing must use pluggable `ISigningProvider` (HSM / vault / in-memory for dev)
3. Nonce management is per-wallet — tracked in Redis to prevent nonce gaps
4. Every transaction must be idempotent — same `task_id` must not submit twice
5. All transactions stored in PostgreSQL `transactions` table before submission

---
