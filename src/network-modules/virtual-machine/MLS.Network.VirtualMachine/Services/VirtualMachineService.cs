using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace MLS.Network.VirtualMachine.Services;

/// <summary>C# Roslyn scripting-based implementation of <see cref="IVirtualMachineService"/>.</summary>
public sealed class VirtualMachineService(ILogger<VirtualMachineService> _logger) : IVirtualMachineService
{
    private readonly ConcurrentDictionary<Guid, SandboxInfo> _sandboxes = new();

    /// <inheritdoc/>
    public async Task<SandboxResult> ExecuteAsync(SandboxRequest request, CancellationToken ct)
    {
        var sandboxId = Guid.NewGuid();
        _sandboxes[sandboxId] = new SandboxInfo(sandboxId, SandboxState.Running,
            DateTimeOffset.UtcNow, null);

        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        var originalOut = Console.Out;
        using var outputWriter = new StringWriter();
        Console.SetOut(outputWriter);

        try
        {
            var options = ScriptOptions.Default
                .WithImports("System", "System.Linq", "System.Collections.Generic");

            await CSharpScript.EvaluateAsync<object?>(
                request.Script, options, cancellationToken: cts.Token).ConfigureAwait(false);

            var output = TruncateOutput(outputWriter.ToString(), request.MaxOutputBytes);
            sw.Stop();
            _sandboxes[sandboxId] = new SandboxInfo(sandboxId, SandboxState.Completed,
                _sandboxes[sandboxId].CreatedAt, DateTimeOffset.UtcNow);
            return new SandboxResult(sandboxId, true, output, null, sw.ElapsedMilliseconds, 0);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            sw.Stop();
            _sandboxes[sandboxId] = new SandboxInfo(sandboxId, SandboxState.TimedOut,
                _sandboxes[sandboxId].CreatedAt, DateTimeOffset.UtcNow);
            return new SandboxResult(sandboxId, false, string.Empty, "Script execution timed out",
                sw.ElapsedMilliseconds, -1);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _sandboxes[sandboxId] = new SandboxInfo(sandboxId, SandboxState.Failed,
                _sandboxes[sandboxId].CreatedAt, DateTimeOffset.UtcNow);
            _logger.LogDebug(ex, "Script execution failed in sandbox {SandboxId}", sandboxId);
            return new SandboxResult(sandboxId, false, string.Empty, ex.Message,
                sw.ElapsedMilliseconds, 1);
        }
        finally
        {
            Console.SetOut(originalOut);
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

    private static string TruncateOutput(string output, int maxBytes)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(output) <= maxBytes) return output;
        return output[..Math.Min(output.Length, maxBytes / 4)] + "...[truncated]";
    }
}
