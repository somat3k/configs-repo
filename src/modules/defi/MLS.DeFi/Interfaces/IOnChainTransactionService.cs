using MLS.DeFi.Models;

namespace MLS.DeFi.Interfaces;

/// <summary>
/// Broadcasts signed transactions to the configured EVM-compatible chain and
/// monitors their confirmation status.
/// </summary>
public interface IOnChainTransactionService
{
    /// <summary>
    /// Resolves the named address from PostgreSQL, signs the transaction via
    /// <see cref="IWalletProvider"/>, and broadcasts it to the chain.
    /// </summary>
    /// <param name="request">Unsigned transaction parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="OnChainTransactionResult"/> with the transaction hash and initial status.
    /// </returns>
    Task<OnChainTransactionResult> SubmitAsync(OnChainTransactionRequest request, CancellationToken ct);

    /// <summary>
    /// Polls the chain for the current status of a previously submitted transaction.
    /// </summary>
    /// <param name="txHash">Transaction hash returned by <see cref="SubmitAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Updated <see cref="OnChainTransactionResult"/>, or <see langword="null"/> when the hash
    /// is not known to the connected node.
    /// </returns>
    Task<OnChainTransactionResult?> GetStatusAsync(string txHash, CancellationToken ct);
}
