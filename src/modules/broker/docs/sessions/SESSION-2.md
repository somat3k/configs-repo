# broker — Session 2: Critical Rules

> Use this document as context when generating Broker module code with GitHub Copilot.

---

## 2. Critical Rules


1. **HYPERLIQUID is the sole primary DEX/perpetuals broker** — no other venue hardcoded
2. All exchange API endpoints and contract addresses loaded from PostgreSQL `blockchain_addresses` — **never hardcoded**
3. Fallback chain: HYPERLIQUID → Broker1 → Broker2 (configured at runtime)
4. Orders must be idempotent — retried orders carry the same client order ID
5. **No Uniswap** — any reference should fail compilation via `.github/copilot-rules/rule-no-uniswap.md`

---
