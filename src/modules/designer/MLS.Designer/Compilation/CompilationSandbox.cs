using System.Runtime.Loader;
using MLS.Core.Designer;

namespace MLS.Designer.Compilation;

/// <summary>
/// Hosts a user-compiled <see cref="IBlockElement"/> inside an isolated
/// <see cref="AssemblyLoadContext"/> with the following runtime restrictions:
/// <list type="bullet">
///   <item>100 ms execution timeout per <see cref="ProcessAsync"/> call.</item>
///   <item>64 MB heap soft limit — checked before each call; throws if exceeded.</item>
///   <item>Full ALC unload on <see cref="DisposeAsync"/> — no managed handles leak.</item>
/// </list>
/// </summary>
public sealed class CompilationSandbox : IAsyncDisposable
{
    // ── Config constants ──────────────────────────────────────────────────────────

    private const int    ProcessTimeoutMs = 100;
    private const long   MemoryLimitBytes = 64 * 1024 * 1024; // 64 MB

    // ── State ─────────────────────────────────────────────────────────────────────

    private readonly BlockAssemblyLoadContext _alc;
    private readonly IBlockElement            _block;
    private          bool                     _disposed;

    // ── Constructor ───────────────────────────────────────────────────────────────

    private CompilationSandbox(BlockAssemblyLoadContext alc, IBlockElement block)
    {
        _alc   = alc;
        _block = block;
    }

    // ── Factory ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Load <paramref name="assemblyBytes"/> into an isolated ALC and instantiate the
    /// first <see cref="IBlockElement"/> implementation found in the assembly.
    /// </summary>
    /// <param name="assemblyBytes">Compiled DLL bytes produced by <see cref="RoslynStrategyCompiler"/>.</param>
    /// <returns>A ready-to-use <see cref="CompilationSandbox"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the assembly contains no public <see cref="IBlockElement"/> implementation.
    /// </exception>
    public static CompilationSandbox Load(byte[] assemblyBytes)
    {
        ArgumentNullException.ThrowIfNull(assemblyBytes);

        var alc      = new BlockAssemblyLoadContext();
        var assembly = alc.LoadFromStream(new MemoryStream(assemblyBytes));

        var blockType = assembly.GetExportedTypes()
            .FirstOrDefault(t => !t.IsAbstract && typeof(IBlockElement).IsAssignableFrom(t))
            ?? throw new InvalidOperationException(
                "The compiled assembly does not contain a public, non-abstract IBlockElement implementation.");

        var block = (IBlockElement)(Activator.CreateInstance(blockType)
            ?? throw new InvalidOperationException($"Could not create instance of '{blockType.FullName}'."));

        return new CompilationSandbox(alc, block);
    }

    // ── Public surface ────────────────────────────────────────────────────────────

    /// <summary>The sandboxed block instance.</summary>
    public IBlockElement Block => _block;

    /// <summary>
    /// Forward a signal to the sandboxed block, enforcing the 100 ms timeout and
    /// the 64 MB memory soft limit.
    /// </summary>
    /// <param name="signal">Incoming block signal.</param>
    /// <param name="ct">Caller cancellation token (combined with internal timeout).</param>
    /// <exception cref="OperationCanceledException">
    /// Thrown when either the caller's <paramref name="ct"/> is cancelled or the
    /// 100 ms per-call timeout fires.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sandbox heap usage exceeds the 64 MB soft limit.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the sandbox has been disposed.</exception>
    public async ValueTask ProcessAsync(BlockSignal signal, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnforceMemoryLimit();

        using var timeoutCts = new CancellationTokenSource(ProcessTimeoutMs);
        using var linked     = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        await _block.ProcessAsync(signal, linked.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Dispose the sandbox: disposes the block, then unloads the ALC so the
    /// compiled assembly can be GC'd.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _block.DisposeAsync().ConfigureAwait(false);
        _alc.Unload();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static void EnforceMemoryLimit()
    {
        // GC.GetTotalMemory measures the entire managed heap for this process, not just this
        // sandbox's ALC.  For the MLS deployment each strategy runs in its own Docker container,
        // so the process limit is a valid proxy for per-sandbox memory.  A per-ALC measurement
        // would require EventSource/EventListener on GC events which adds complexity without
        // meaningful benefit in a container-per-strategy topology.
        var used = GC.GetTotalMemory(forceFullCollection: false);
        if (used > MemoryLimitBytes)
            throw new InvalidOperationException(
                $"Sandbox memory limit exceeded: {used / (1024 * 1024)} MB used (limit {MemoryLimitBytes / (1024 * 1024)} MB).");
    }

    // ── Inner: isolated ALC ───────────────────────────────────────────────────────

    /// <summary>
    /// Collector-unloadable <see cref="AssemblyLoadContext"/> for user block assemblies.
    /// Parent context provides the BCL and MLS.Core references; nothing else leaks in.
    /// </summary>
    private sealed class BlockAssemblyLoadContext() : AssemblyLoadContext(
        name: $"UserBlock-{Guid.NewGuid():N}",
        isCollectible: true)
    {
        protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName assemblyName)
        {
            // Resolve to the default (host) context for all trusted assemblies.
            // User assembly bytes are loaded explicitly via LoadFromStream.
            return null;
        }
    }
}
