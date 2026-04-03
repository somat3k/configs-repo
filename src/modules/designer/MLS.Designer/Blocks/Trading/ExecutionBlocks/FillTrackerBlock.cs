using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.ExecutionBlocks;

/// <summary>
/// Fill tracker block that awaits <see cref="BlockSocketType.OrderResult"/> confirmations
/// and re-emits a <see cref="BlockSocketType.TradeOrder"/> retry if the fill is rejected.
/// </summary>
public sealed class FillTrackerBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("result_input", BlockSocketType.OrderResult),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("fill_output", BlockSocketType.OrderResult),
        BlockSocket.Output("retry_output", BlockSocketType.TradeOrder),
    ];

    private readonly BlockParameter<int> _maxRetriesParam = new("MaxRetries", "Max Retries", "Maximum fill retry attempts", 3, MinValue: 0, MaxValue: 10);

    private readonly Dictionary<string, int> _retryCount = [];

    /// <inheritdoc/>
    public override string BlockType   => "FillTrackerBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Fill Tracker";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_maxRetriesParam];

    /// <summary>Initialises a new <see cref="FillTrackerBlock"/>.</summary>
    public FillTrackerBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset() => _retryCount.Clear();

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.OrderResult)
            return new ValueTask<BlockSignal?>(result: null);

        var (orderId, status, _) = ExtractResult(signal.Value);

        if (status == "filled")
        {
            _retryCount.Remove(orderId);
            return new ValueTask<BlockSignal?>(
                EmitObject(BlockId, "fill_output", BlockSocketType.OrderResult, signal.Value));
        }

        // Rejected: retry if under limit
        _retryCount.TryGetValue(orderId, out var retries);

        if (retries >= _maxRetriesParam.DefaultValue)
        {
            _retryCount.Remove(orderId);
            return new ValueTask<BlockSignal?>(
                EmitObject(BlockId, "fill_output", BlockSocketType.OrderResult, signal.Value)); // emit final reject
        }

        _retryCount[orderId] = retries + 1;
        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "retry_output", BlockSocketType.TradeOrder, signal.Value));
    }

    private static (string orderId, string status, string reason) ExtractResult(JsonElement value)
    {
        var orderId = value.ValueKind == JsonValueKind.Object && value.TryGetProperty("order_id", out var o) ? o.GetString() ?? "" : "";
        var status  = value.ValueKind == JsonValueKind.Object && value.TryGetProperty("status",   out var s) ? s.GetString() ?? "" : "";
        var reason  = value.ValueKind == JsonValueKind.Object && value.TryGetProperty("reason",   out var r) ? r.GetString() ?? "" : "";
        return (orderId, status, reason);
    }
}
