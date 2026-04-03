using System.Collections.Concurrent;
using MLS.Core.Designer;

namespace MLS.Designer.Services;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IBlockRegistry"/>.
/// All Trading-domain blocks are registered during application startup.
/// </summary>
public sealed class BlockRegistry : IBlockRegistry
{
    private readonly ConcurrentDictionary<string, BlockRegistration> _entries = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void Register<T>(string key) where T : IBlockElement, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // Create a throw-away prototype to read metadata; DisposeAsync is a no-op for all
        // concrete blocks in this project (they hold no unmanaged resources at construction).
        var prototype = new T();
        var metadata  = new BlockMetadata(
            Key:               key,
            DisplayName:       prototype.DisplayName,
            Category:          DeriveCategory(key),
            Description:       $"{prototype.DisplayName} block",
            InputSocketNames:  prototype.InputSockets.Select(s => s.Name).ToList(),
            OutputSocketNames: prototype.OutputSockets.Select(s => s.Name).ToList());

        // ValueTask disposal — synchronous path is safe here because BlockBase.DisposeAsync
        // returns ValueTask.CompletedTask; no blocking occurs.
        var disposeTask = prototype.DisposeAsync();
        if (!disposeTask.IsCompleted)
            disposeTask.AsTask().GetAwaiter().GetResult();

        _entries[key] = new BlockRegistration(metadata, () => new T());
    }

    /// <inheritdoc/>
    public void Register(string key, Func<IBlockElement> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var prototype = factory();
        var metadata  = new BlockMetadata(
            Key:               key,
            DisplayName:       prototype.DisplayName,
            Category:          DeriveCategory(key),
            Description:       $"{prototype.DisplayName} block",
            InputSocketNames:  prototype.InputSockets.Select(s => s.Name).ToList(),
            OutputSocketNames: prototype.OutputSockets.Select(s => s.Name).ToList());

        var disposeTask = prototype.DisposeAsync();
        if (!disposeTask.IsCompleted)
            disposeTask.AsTask().GetAwaiter().GetResult();

        _entries[key] = new BlockRegistration(metadata, factory);
    }

    /// <inheritdoc/>
    public IReadOnlyList<BlockMetadata> GetAll() =>
        _entries.Values.Select(r => r.Metadata).OrderBy(m => m.Category).ThenBy(m => m.Key).ToList();

    /// <inheritdoc/>
    public BlockMetadata? GetByKey(string key) =>
        _entries.TryGetValue(key, out var reg) ? reg.Metadata : null;

    /// <inheritdoc/>
    public IBlockElement? CreateInstance(string key) =>
        _entries.TryGetValue(key, out var reg) ? reg.Factory() : null;

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static string DeriveCategory(string key) => key switch
    {
        _ when key.EndsWith("FeedBlock",    StringComparison.Ordinal) => "DataSource",
        _ when key.EndsWith("ReplayBlock",  StringComparison.Ordinal) => "DataSource",
        _ when key.Contains("RSI",         StringComparison.Ordinal)
            || key.Contains("MACD",        StringComparison.Ordinal)
            || key.Contains("Bollinger",   StringComparison.Ordinal)
            || key.Contains("ATR",         StringComparison.Ordinal)
            || key.Contains("VWAP",        StringComparison.Ordinal)
            || key.Contains("Volume",      StringComparison.Ordinal)
            || key.Contains("Indicator",   StringComparison.Ordinal) => "Indicator",
        _ when key.Contains("Model",       StringComparison.Ordinal)
            || key.Contains("Ensemble",    StringComparison.Ordinal)
            || key.Contains("Inference",   StringComparison.Ordinal) => "ML",
        _ when key.Contains("Strategy",   StringComparison.Ordinal)
            || key.Contains("Momentum",    StringComparison.Ordinal)
            || key.Contains("MeanReversion", StringComparison.Ordinal)
            || key.Contains("TrendFollow", StringComparison.Ordinal)
            || key.Contains("Composite",   StringComparison.Ordinal) => "Strategy",
        _ when key.Contains("Risk",        StringComparison.Ordinal)
            || key.Contains("Position",    StringComparison.Ordinal)
            || key.Contains("StopLoss",    StringComparison.Ordinal)
            || key.Contains("Drawdown",    StringComparison.Ordinal)
            || key.Contains("Exposure",    StringComparison.Ordinal) => "Risk",
        _ when key.Contains("Order",       StringComparison.Ordinal)
            || key.Contains("Fill",        StringComparison.Ordinal)
            || key.Contains("Slippage",    StringComparison.Ordinal)
            || key.Contains("Router",      StringComparison.Ordinal)
            || key.Contains("Emitter",     StringComparison.Ordinal) => "Execution",
        _                                                              => "Other",
    };

    // ── Nested types ──────────────────────────────────────────────────────────────

    private sealed record BlockRegistration(BlockMetadata Metadata, Func<IBlockElement> Factory);
}
