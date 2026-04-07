using System.Text.Json;
using Microsoft.Extensions.Options;
using MLS.Core.Designer;
using MLS.Designer.Configuration;

namespace MLS.Designer.Compilation;

/// <summary>
/// Downloads a pre-compiled user block assembly from IPFS by its Content Identifier (CID)
/// and loads it into a <see cref="CompilationSandbox"/>.
/// </summary>
/// <remarks>
/// <para>
/// The IPFS gateway/node is called via the Kubo HTTP API endpoint
/// <c>GET /api/v0/cat?arg={cid}</c>.  Configure the endpoint via
/// <see cref="DesignerOptions.IpfsApiUrl"/> (default: <c>http://ipfs:5001</c>).
/// </para>
/// <para>
/// Each loaded sandbox is independent and collectible — call
/// <see cref="IAsyncDisposable.DisposeAsync"/> when the strategy stops to free the
/// compiled assembly and its associated <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
/// </para>
/// </remarks>
public sealed class DynamicBlockLoader(
    IHttpClientFactory httpClientFactory,
    IOptions<DesignerOptions> options,
    ILogger<DynamicBlockLoader> logger)
{
    private const string IpfsHttpClient = "ipfs";

    // ── Public API ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Download the assembly identified by <paramref name="cid"/> from IPFS and load it
    /// into a new <see cref="CompilationSandbox"/>.
    /// </summary>
    /// <param name="cid">IPFS CID, e.g. <c>"bafybeig…"</c> or legacy <c>"Qm…"</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A ready-to-use <see cref="CompilationSandbox"/> wrapping the loaded block.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cid"/> is null or empty.</exception>
    /// <exception cref="HttpRequestException">Thrown when the IPFS download fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the assembly has no valid block type.</exception>
    public async Task<CompilationSandbox> LoadFromCidAsync(string cid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cid))
            throw new ArgumentException("CID must not be null or whitespace.", nameof(cid));

        var ipfsApiUrl = options.Value.IpfsApiUrl;
        if (string.IsNullOrWhiteSpace(ipfsApiUrl))
            throw new InvalidOperationException("Designer:IpfsApiUrl is not configured.");

        logger.LogInformation("Loading block assembly from IPFS CID={Cid}", cid);

        var bytes = await DownloadFromIpfsAsync(ipfsApiUrl, cid, ct).ConfigureAwait(false);

        logger.LogInformation("Downloaded {Size} bytes for CID={Cid}; loading sandbox", bytes.Length, cid);

        var sandbox = CompilationSandbox.Load(bytes);
        logger.LogInformation("Sandbox loaded: block type = {BlockType}", sandbox.Block.BlockType);
        return sandbox;
    }

    /// <summary>
    /// Verify that a CID is reachable on the IPFS node without fully downloading the assembly.
    /// Uses the <c>/api/v0/object/stat</c> endpoint.
    /// </summary>
    /// <param name="cid">IPFS CID to probe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the object exists; <see langword="false"/> otherwise.</returns>
    public async Task<bool> CidExistsAsync(string cid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cid)) return false;

        var ipfsApiUrl = options.Value.IpfsApiUrl;
        if (string.IsNullOrWhiteSpace(ipfsApiUrl)) return false;

        try
        {
            using var client = httpClientFactory.CreateClient(IpfsHttpClient);
            var uri      = new Uri(new Uri(ipfsApiUrl), $"/api/v0/object/stat?arg={Uri.EscapeDataString(cid)}");
            using var response = await client.PostAsync(uri, null, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "CID existence check failed for {Cid}", cid);
            return false;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<byte[]> DownloadFromIpfsAsync(string ipfsApiUrl, string cid, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient(IpfsHttpClient);
        // Kubo HTTP API uses POST for all /api/v0/* endpoints — this is by design.
        // See: https://docs.ipfs.tech/reference/kubo/rpc/#api-v0-cat
        var uri = new Uri(new Uri(ipfsApiUrl), $"/api/v0/cat?arg={Uri.EscapeDataString(cid)}");

        using var response = await client.PostAsync(uri, null, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

        if (bytes.Length == 0)
            throw new InvalidOperationException($"IPFS returned empty response for CID '{cid}'.");

        return bytes;
    }
}
