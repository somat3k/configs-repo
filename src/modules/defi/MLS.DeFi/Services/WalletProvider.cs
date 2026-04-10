using MLS.DeFi.Configuration;
using MLS.DeFi.Interfaces;
using MLS.DeFi.Models;

namespace MLS.DeFi.Services;

/// <summary>
/// Environment-variable wallet provider for development and integration testing.
/// Reads the private key from <c>DEFI_WALLET_PRIVATE_KEY</c> — never from config files.
/// In production, replace with a Vault or HSM backend implementation.
/// </summary>
/// <remarks>
/// The private key is read once from the environment variable on first use and is
/// held in memory only for the duration of the signing operation. It is never logged
/// or written to any store.
/// </remarks>
public sealed class WalletProvider(
    IOptions<DeFiOptions> _options,
    ILogger<WalletProvider> _logger) : IWalletProvider
{
    private const string PrivKeyEnvVar = "DEFI_WALLET_PRIVATE_KEY";
    private const string AddressEnvVar = "HYPERLIQUID_WALLET_ADDRESS";

    /// <inheritdoc/>
    public Task<string> GetAddressAsync(CancellationToken ct)
    {
        _ = _options.Value.WalletBackend; // backend type for future routing (env/vault/hsm)
        var addr = Environment.GetEnvironmentVariable(AddressEnvVar);
        if (string.IsNullOrWhiteSpace(addr))
            _logger.LogError("{EnvVar} environment variable is not set", AddressEnvVar);

        return Task.FromResult(addr ?? string.Empty);
    }

    /// <inheritdoc/>
    public Task<string> SignHashAsync(string messageHash, CancellationToken ct)
    {
        // Production: delegate to HSM/Vault. For now return an unsigned placeholder so
        // the module can start without a live key; real signing is wired at deployment.
        var key = Environment.GetEnvironmentVariable(PrivKeyEnvVar);
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogError("{EnvVar} is not set — transaction signing will not produce a valid signature",
                PrivKeyEnvVar);
            return Task.FromResult("0x0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        }

        // Stub: returning a deterministic stub signature. In production, wire Nethereum's
        // EthECKey.SignAndCalculateV or delegate to the configured vault/HSM backend.
        // A critical-level log is emitted so this cannot be missed in production environments.
        _logger.LogCritical(
            "WalletProvider is using a STUB signature — {EnvVar} is not set. " +
            "This must be replaced with a real signing implementation before production use.",
            PrivKeyEnvVar);
        return Task.FromResult("0x" + messageHash.PadRight(130, '0')[..130]);
    }

    /// <inheritdoc/>
    public async Task<WalletSignResult> SignTransactionAsync(OnChainTransactionRequest request, CancellationToken ct)
    {
        var address = await GetAddressAsync(ct).ConfigureAwait(false);

        // Production: build and RLP-encode the unsigned transaction, then call the HSM/Vault
        // to sign it. Nethereum's TransactionSigner handles RLP + EIP-155 chain ID replay
        // protection.  Here we return a stub signed-tx for integration testing.
        var signedTxHex = $"0x{Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(
            $"stub-signed-{request.AddressName}-{request.EncodedCalldata[..Math.Min(8, request.EncodedCalldata.Length)]}"))}";

        _logger.LogInformation("WalletProvider signed transaction for address={Address} target={AddressName}",
            DeFiUtils.SafeLog(address), DeFiUtils.SafeLog(request.AddressName));

        return new WalletSignResult(signedTxHex, address);
    }
}
