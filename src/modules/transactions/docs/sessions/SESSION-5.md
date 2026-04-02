# transactions — Session 5: Data Models

> Use this document as context when generating Transactions module code with GitHub Copilot.

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
