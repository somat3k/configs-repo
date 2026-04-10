using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace MLS.Network.VirtualMachine.Services;

/// <summary>
/// In-process C# Roslyn scripting sandbox.
/// <para>
/// <b>Security notice:</b> Scripts run in the same process as the host with default CLR permissions.
/// This service is intended for <b>trusted internal callers only</b>. Untrusted user-supplied scripts
/// must be executed in an isolated out-of-process container instead.
/// </para>
/// </summary>
public sealed class VirtualMachineService(ILogger<VirtualMachineService> _logger) : IVirtualMachineService
{
    private readonly ConcurrentDictionary<Guid, SandboxInfo> _sandboxes = new();

    /// <inheritdoc/>
    public async Task<SandboxResult> ExecuteAsync(SandboxRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // Reject assembly-loading / file-include directives that bypass import restrictions.
        if (request.Script.Contains("#r ", StringComparison.Ordinal) ||
            request.Script.Contains("#load ", StringComparison.Ordinal))
        {
            sw.Stop();
            return new SandboxResult(Guid.Empty, false, string.Empty,
                "Script directives #r and #load are not permitted.", sw.ElapsedMilliseconds, 1);
        }

        var sandboxId  = Guid.NewGuid();
        var createdAt  = DateTimeOffset.UtcNow;
        _sandboxes[sandboxId] = new SandboxInfo(sandboxId, SandboxState.Running, createdAt, null);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        try
        {
            var options = ScriptOptions.Default
                .WithImports("System", "System.Linq", "System.Collections.Generic")
                .WithAllowUnsafe(false);

            var returnValue = await CSharpScript.EvaluateAsync<object?>(
                request.Script, options, cancellationToken: cts.Token).ConfigureAwait(false);

            var output = TruncateOutput(returnValue?.ToString() ?? string.Empty, request.MaxOutputBytes);
            sw.Stop();
            _sandboxes[sandboxId] = new SandboxInfo(sandboxId, SandboxState.Completed, createdAt, DateTimeOffset.UtcNow);
            return new SandboxResult(sandboxId, true, output, null, sw.ElapsedMilliseconds, 0);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            sw.Stop();
            _sandboxes[sandboxId] = new SandboxInfo(sandboxId, SandboxState.TimedOut, createdAt, DateTimeOffset.UtcNow);
            return new SandboxResult(sandboxId, false, string.Empty, "Script execution timed out",
                sw.ElapsedMilliseconds, -1);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _sandboxes[sandboxId] = new SandboxInfo(sandboxId, SandboxState.Failed, createdAt, DateTimeOffset.UtcNow);
            _logger.LogDebug(ex, "Script execution failed in sandbox {SandboxId}", sandboxId);
            return new SandboxResult(sandboxId, false, string.Empty, ex.Message,
                sw.ElapsedMilliseconds, 1);
        }
    }

    /// <inheritdoc/>
    public Task<SandboxInfo?> GetSandboxAsync(Guid sandboxId, CancellationToken ct) =>
        Task.FromResult(_sandboxes.TryGetValue(sandboxId, out var info) ? info : null);

    /// <inheritdoc/>
    public async IAsyncEnumerable<SandboxInfo> GetActiveSandboxesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var info in _sandboxes.Values)
        {
            ct.ThrowIfCancellationRequested();
            yield return info;
            await Task.Yield();
        }
    }

    /// <inheritdoc/>
    public Task TerminateSandboxAsync(Guid sandboxId, CancellationToken ct)
    {
        _sandboxes.TryRemove(sandboxId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Truncates <paramref name="output"/> so its UTF-8 encoding does not exceed
    /// <paramref name="maxBytes"/> bytes, appending <c>...[truncated]</c> when cut.
    /// </summary>
    private static string TruncateOutput(string output, int maxBytes)
    {
        if (maxBytes <= 0) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(output);
        if (bytes.Length <= maxBytes) return output;
        // Decode safely — GetString handles partial multibyte sequences gracefully.
        return Encoding.UTF8.GetString(bytes, 0, maxBytes) + "...[truncated]";
    }
}
