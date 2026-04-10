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
        var key = Environment.GetEnvironmentVariable(PrivKeyEnvVar);
        if (string.IsNullOrWhiteSpace(key))
        {
            // Key is absent — log at Error so the misconfiguration is visible,
            // and return an all-zero placeholder to allow the module to start.
            _logger.LogError("{EnvVar} is not set — transaction signing will not produce a valid signature",
                PrivKeyEnvVar);
            return Task.FromResult("0x0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        }

        // Key is present but real EVM signing (Nethereum EthECKey.SignAndCalculateV or HSM
        // delegation) is not yet wired.  Log at Critical so this cannot be missed in staging
        // or production before the real backend is configured.
        _logger.LogCritical(
            "WalletProvider is using a STUB signature implementation. " +
            "Wire a real EthECKey / Vault / HSM backend before production use.");
        return Task.FromResult("0x" + messageHash.PadRight(130, '0')[..130]);
    }

    /// <inheritdoc/>
    public async Task<WalletSignResult> SignTransactionAsync(
        OnChainTransactionRequest request,
        string toAddress,
        CancellationToken ct)
    {
        var fromAddress = await GetAddressAsync(ct).ConfigureAwait(false);

        // Production: build the RLP-encoded unsigned transaction using `toAddress` as the
        // destination, then sign with Nethereum's TransactionSigner (EIP-155 chain ID).
        // Here we return a stub for integration testing — toAddress is included in the
        // stub payload so callers can verify it flows through correctly.
        var signedTxHex = $"0x{Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(
            $"stub-signed-to:{toAddress}-{request.AddressName}-{request.EncodedCalldata[..Math.Min(8, request.EncodedCalldata.Length)]}"))}";

        _logger.LogInformation(
            "WalletProvider signed transaction: from={From} to={To} addressName={Name}",
            DeFiUtils.SafeLog(fromAddress),
            DeFiUtils.SafeLog(toAddress),
            DeFiUtils.SafeLog(request.AddressName));

        return new WalletSignResult(signedTxHex, fromAddress);
    }
}
