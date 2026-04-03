# transactions — Session 7: Message Flow

> Use this document as context when generating Transactions module code with GitHub Copilot.

---

## 7. Message Flow


```
Broker → BC (TX_CREATE)
       → BC routes → Transactions HTTP POST /api/transactions
       ← Transactions: 202 Accepted { tx_id }

Transactions: INonceManager.GetNextNonceAsync → Redis
Transactions: ISigningProvider.SignAsync → SignedTransaction
Transactions: ITransactionSubmitter.SubmitAsync → blockchain RPC
Transactions → BC (TX_SUBMITTED: tx_hash)

Transactions: poll receipt via MonitorAsync (IAsyncEnumerable)
Transactions → BC (TX_CONFIRMED: receipt) or (TX_FAILED)
```

---
