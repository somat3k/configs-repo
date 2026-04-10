using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace MLS.ShellVM.Services;

/// <summary>
/// Provides PTY-style process management using <see cref="System.Diagnostics.Process"/>
/// with redirected stdin/stdout/stderr streams.
/// </summary>
/// <remarks>
/// True PTY (pseudo-terminal) requires native OS APIs not available in managed .NET.
/// This implementation uses redirected streams to achieve equivalent functionality
/// for script execution, diagnostics, and non-interactive commands.
/// </remarks>
public sealed class PtyProviderService(
    IOptions<ShellVMConfig> _config,
    ILogger<PtyProviderService> _logger) : IPtyProvider, IDisposable
{
    private readonly ConcurrentDictionary<int, Process> _processes = new();

    /// <summary>Allow-listed executable values from configuration.</summary>
    private IReadOnlySet<string> AllowedExecutables =>
        new HashSet<string>(_config.Value.AllowedShells, StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<PtyHandle> SpawnAsync(PtySpawnOptions options, CancellationToken ct)
    {
        // Defense-in-depth: validate the executable against the allow-list even though
        // SessionManager already performs this check at session creation time.
        if (!AllowedExecutables.Contains(options.Executable))
            throw new InvalidOperationException(
                $"Executable '{SanitiseForLog(options.Executable)}' is not in the allow-list. " +
                "Refusing to spawn process.");

        var psi = new ProcessStartInfo
        {
            FileName               = options.Executable,
            WorkingDirectory       = options.WorkingDirectory,
            UseShellExecute        = false,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };

        foreach (var (key, value) in options.Environment)
            psi.Environment[key] = value;

        foreach (var arg in options.Arguments)
            psi.ArgumentList.Add(arg);

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            process.Dispose();
            throw new InvalidOperationException(
                $"Failed to start process '{options.Executable}': {ex.Message}", ex);
        }

        _processes[process.Id] = process;
        _logger.LogDebug("Spawned process {Pid} ({Exe})", process.Id, options.Executable);

        var handle = new PtyHandle(
            ProcessId:   process.Id,
            ProcessName: options.Executable,
            Cols:        options.Cols,
            Rows:        options.Rows);

        return Task.FromResult(handle);
    }

    /// <inheritdoc/>
    public async ValueTask WriteInputAsync(
        PtyHandle handle, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (!_processes.TryGetValue(handle.ProcessId, out var process))
        {
            _logger.LogWarning("WriteInputAsync: process {Pid} not found", handle.ProcessId);
            return;
        }

        var text = Encoding.UTF8.GetString(data.Span);
        await process.StandardInput.WriteAsync(text.AsMemory(), ct).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<OutputChunk> ReadOutputAsync(
        PtyHandle handle,
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_processes.TryGetValue(handle.ProcessId, out var process))
            yield break;

        long sequence = 0;
        var  buffer   = new char[4096];

        // Read stdout and stderr concurrently using two Tasks feeding into a shared channel
        var outputChannel = System.Threading.Channels.Channel.CreateBounded<OutputChunk>(
            new System.Threading.Channels.BoundedChannelOptions(1024)
            {
                FullMode     = System.Threading.Channels.BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            });

        var stdoutTask = ReadStreamAsync(
            process.StandardOutput, OutputStream.Stdout, outputChannel.Writer, sessionId, ct);
        var stderrTask = ReadStreamAsync(
            process.StandardError, OutputStream.Stderr, outputChannel.Writer, sessionId, ct);

        // Complete writer when both readers finish
        _ = Task.WhenAll(stdoutTask, stderrTask)
                .ContinueWith(_ => outputChannel.Writer.TryComplete(), CancellationToken.None,
                    TaskContinuationOptions.None, TaskScheduler.Default);

        await foreach (var chunk in outputChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return chunk with { Sequence = Interlocked.Increment(ref sequence) };
        }
    }

    /// <inheritdoc/>
    public ValueTask ResizeAsync(PtyHandle handle, int cols, int rows, CancellationToken ct)
    {
        // Resizing requires native ioctl(TIOCSWINSZ) — not available without native interop.
        // Log the request; a future native PTY integration can honour it.
        _logger.LogDebug("Resize PTY for process {Pid} to {Cols}×{Rows} (no-op in managed mode)",
            handle.ProcessId, cols, rows);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask KillAsync(PtyHandle handle, CancellationToken ct)
    {
        if (!_processes.TryRemove(handle.ProcessId, out var process))
            return ValueTask.CompletedTask;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.LogDebug("Killed process {Pid}", handle.ProcessId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kill failed for process {Pid}", handle.ProcessId);
        }
        finally
        {
            process.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<int> WaitForExitAsync(PtyHandle handle, CancellationToken ct)
    {
        if (!_processes.TryGetValue(handle.ProcessId, out var process))
            return -1;

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        _processes.TryRemove(handle.ProcessId, out _);
        return process.ExitCode;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var process in _processes.Values)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                process.Dispose();
            }
            catch
            {
                // best-effort cleanup
            }
        }
        _processes.Clear();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task ReadStreamAsync(
        System.IO.TextReader reader,
        OutputStream stream,
        System.Threading.Channels.ChannelWriter<OutputChunk> writer,
        Guid sessionId,
        CancellationToken ct)
    {
        var buffer = new char[4096];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                var data  = new string(buffer, 0, read);
                var chunk = new OutputChunk(
                    SessionId: sessionId,
                    Stream:    stream,
                    Data:      data,
                    Sequence:  0,   // overwritten by caller
                    Timestamp: DateTimeOffset.UtcNow);
                await writer.WriteAsync(chunk, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
    }

    /// <summary>Strips CR/LF from a user-supplied string before it enters a log message or exception text.</summary>
    private static string SanitiseForLog(string? value) =>
        (value ?? string.Empty).Replace('\r', '_').Replace('\n', '_');
}
