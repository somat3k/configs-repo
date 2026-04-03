# Transactions Module — Session Prompt

> Use this document as context when generating Transactions module code with GitHub Copilot.

---

## 1. Module Identity

| Field | Value |
|---|---|
| **Name** | `transactions` |
| **Namespace** | `MLS.Transactions` |
| **Role** | Blockchain transaction lifecycle — construction, signing, submission, monitoring |
| **HTTP Port** | `5900` |
| **WebSocket Port** | `6900` |
| **Container** | `mls-transactions` |
| **Docker image** | `ghcr.io/somat3k/mls-transactions:latest` |

---

## 2. Critical Rules

1. All contract addresses and RPC endpoints loaded from PostgreSQL `blockchain_addresses` — **never hardcoded**
2. Transaction signing must use pluggable `ISigningProvider` (HSM / vault / in-memory for dev)
3. Nonce management is per-wallet — tracked in Redis to prevent nonce gaps
4. Every transaction must be idempotent — same `task_id` must not submit twice
5. All transactions stored in PostgreSQL `transactions` table before submission

---

## 3. Required Interfaces

```csharp
namespace MLS.Transactions.Interfaces;

/// <summary>Manages the full lifecycle of a blockchain transaction.</summary>
public interface ITransactionManager
{
    Task<PendingTransaction> CreateAsync(TransactionRequest request, CancellationToken ct);
    Task<TransactionReceipt?> GetReceiptAsync(string txHash, CancellationToken ct);
    Task<IReadOnlyList<PendingTransaction>> GetPendingAsync(CancellationToken ct);
    IAsyncEnumerable<TransactionReceipt> MonitorAsync(string txHash, CancellationToken ct);
}

/// <summary>Signs transaction data using the configured signing backend.</summary>
public interface ISigningProvider
{
    Task<SignedTransaction> SignAsync(UnsignedTransaction tx, string walletAddress, CancellationToken ct);
}

/// <summary>Tracks and allocates nonces per wallet address to prevent gaps.</summary>
public interface INonceManager
{
    Task<ulong> GetNextNonceAsync(string walletAddress, CancellationToken ct);
    Task ConfirmNonceAsync(string walletAddress, ulong nonce, CancellationToken ct);
    Task ReleaseNonceAsync(string walletAddress, ulong nonce, CancellationToken ct);
}

/// <summary>Submits signed transactions to the blockchain RPC node.</summary>
public interface ITransactionSubmitter
{
    Task<string> SubmitAsync(SignedTransaction tx, CancellationToken ct);
    Task<TransactionReceipt?> WaitForReceiptAsync(string txHash, TimeSpan timeout, CancellationToken ct);
}

/// <summary>Reliable submission queue with retry and dead-letter handling.</summary>
public interface ITransactionQueue
{
    ValueTask EnqueueAsync(PendingTransaction tx, CancellationToken ct);
    IAsyncEnumerable<PendingTransaction> DequeueAsync(CancellationToken ct);
    Task DeadLetterAsync(PendingTransaction tx, string reason, CancellationToken ct);
}
```

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

## 5. Data Models

```csharp
namespace MLS.Transactions.Models;

public sealed record TransactionRequest(
    string FromAddress,
    string ToAddress,
    string? Data,               // Hex-encoded calldata (null for ETH transfer)
    decimal? ValueEth,
    ulong? GasLimit,
    decimal? MaxFeePerGas,
    decimal? MaxPriorityFeePerGas,
    string TaskId,              // Idempotency key from Block Controller
    string RequestingModuleId
);

public sealed record PendingTransaction(
    Guid Id,
    TransactionRequest Request,
    SignedTransaction? Signed,
    TransactionState State,
    string? TxHash,
    int RetryCount,
    DateTimeOffset CreatedAt
);

public sealed record TransactionReceipt(
    string TxHash,
    bool Success,
    ulong BlockNumber,
    ulong GasUsed,
    DateTimeOffset MinedAt
);

public enum TransactionState { Queued, Signing, Submitted, Pending, Confirmed, Failed, DeadLetter }
```

---

## 6. Database Dependencies

| Table | Purpose |
|---|---|
| `blockchain_addresses` | RPC endpoints, contract addresses |
| `transactions` | Full transaction lifecycle (EF Core entity) |
| `transaction_receipts` | Confirmed receipts with block data |

Redis is used for nonce tracking only (key: `nonce:{walletAddress}`).

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

## 8. Skills to Apply

- `.skills/web3.md` — RPC submission, nonce management, receipt monitoring
- `.skills/networking.md` — WebSocket server, Block Controller registration, heartbeat
- `.skills/websockets-inferences.md` — Envelope protocol, SignalR hub
- `.skills/storage-data-management.md` — EF Core transaction table, Redis nonce cache
- `.skills/beast-development.md` — `Channel<T>` queue, retry backoff, circuit breaker
