using MLS.DeFi.Models;

namespace MLS.DeFi.Interfaces;

/// <summary>
/// Pluggable wallet backend abstraction for signing on-chain transactions.
/// Concrete implementations may use environment-variable keys (development),
/// HashiCorp Vault (staging/production), or an HSM (production).
/// </summary>
/// <remarks>
/// Private keys are NEVER stored in code or configuration files.
/// This interface decouples signing from key management.
/// </remarks>
public interface IWalletProvider
{
    /// <summary>
    /// Returns the wallet address managed by this provider.
    /// </summary>
    Task<string> GetAddressAsync(CancellationToken ct);

    /// <summary>
    /// Signs an arbitrary message hash using the managed private key.
    /// </summary>
    /// <param name="messageHash">32-byte keccak256 message hash (hex, without 0x prefix).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>EVM-compatible signature (r, s, v encoded as 65-byte hex).</returns>
    Task<string> SignHashAsync(string messageHash, CancellationToken ct);

    /// <summary>
    /// Signs and encodes a raw EVM transaction for broadcast.
    /// </summary>
    /// <param name="request">The unsigned transaction parameters.</param>
    /// <param name="toAddress">
    /// The resolved on-chain destination address (e.g., contract address from the address book).
    /// Passed separately so the wallet provider can include it in the RLP-encoded transaction
    /// without performing its own address-book lookup.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="WalletSignResult"/> containing the RLP-encoded signed transaction.
    /// </returns>
    Task<WalletSignResult> SignTransactionAsync(OnChainTransactionRequest request, string toAddress, CancellationToken ct);
}
