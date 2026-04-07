namespace MLS.Designer.Compilation;

/// <summary>
/// Result of a Roslyn compilation attempt for a user-authored custom block.
/// </summary>
/// <param name="Success">Whether the compilation produced a valid assembly.</param>
/// <param name="AssemblyBytes">
/// The compiled DLL bytes, or <see langword="null"/> on failure.
/// Consumers load these bytes into a <see cref="CompilationSandbox"/>.
/// </param>
/// <param name="Diagnostics">
/// Human-readable compiler messages (errors and warnings), in source order.
/// </param>
/// <param name="IpfsCid">
/// IPFS Content Identifier for the uploaded assembly, set by
/// <see cref="IStrategyCompiler.CompileAndUploadAsync"/> on success.
/// <see langword="null"/> when the result comes from <see cref="IStrategyCompiler.CompileAsync"/>
/// or when the upload was skipped / failed.
/// </param>
/// <param name="ElapsedTime">Wall-clock compilation duration.</param>
public sealed record CompilationResult(
    bool Success,
    byte[]? AssemblyBytes,
    IReadOnlyList<string> Diagnostics,
    string? IpfsCid,
    TimeSpan ElapsedTime);

/// <summary>
/// Compiles user-authored C# indicator/strategy code using Roslyn and optionally
/// publishes the resulting assembly to IPFS for distributed strategy loading.
/// </summary>
/// <remarks>
/// <para>
/// The compiler enforces the MLS sandbox security policy at the source level:
/// <list type="bullet">
///   <item>No <c>System.IO</c> filesystem access.</item>
///   <item>No direct network access (<c>HttpClient</c>, <c>Socket</c>, <c>WebClient</c>).</item>
///   <item>No reflection over MLS internals.</item>
///   <item>Only MLS.Core.Designer contracts are exposed as references.</item>
/// </list>
/// Forbidden API usage is rejected with a meaningful compiler diagnostic before
/// IL emission begins — the source never reaches the .NET runtime.
/// </para>
/// </remarks>
public interface IStrategyCompiler
{
    /// <summary>
    /// Compile <paramref name="csharpSource"/> into an in-memory assembly.
    /// </summary>
    /// <param name="csharpSource">
    /// Full C# compilation unit. Must contain exactly one <c>public sealed class</c>
    /// that extends <c>MLS.Designer.Blocks.BlockBase</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="CompilationResult"/> with <c>IpfsCid = null</c>.
    /// On success <see cref="CompilationResult.AssemblyBytes"/> contains the DLL.
    /// On failure <see cref="CompilationResult.Diagnostics"/> lists all errors.
    /// </returns>
    Task<CompilationResult> CompileAsync(string csharpSource, CancellationToken ct);

    /// <summary>
    /// Compile <paramref name="csharpSource"/> and, on success, upload the assembly to IPFS.
    /// </summary>
    /// <param name="csharpSource">Full C# compilation unit (same contract as <see cref="CompileAsync"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="CompilationResult"/>; on success <see cref="CompilationResult.IpfsCid"/>
    /// is the IPFS Content Identifier of the uploaded DLL.
    /// </returns>
    Task<CompilationResult> CompileAndUploadAsync(string csharpSource, CancellationToken ct);

    /// <summary>
    /// Upload a pre-compiled assembly to IPFS and return its CID.
    /// </summary>
    /// <param name="assemblyBytes">Raw DLL bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>IPFS CID string (e.g. <c>"QmXxx…"</c>).</returns>
    Task<string> UploadAssemblyAsync(byte[] assemblyBytes, CancellationToken ct);
}
