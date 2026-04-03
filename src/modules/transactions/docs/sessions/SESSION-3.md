# transactions — Session 3: Required Interfaces

> Use this document as context when generating Transactions module code with GitHub Copilot.

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
