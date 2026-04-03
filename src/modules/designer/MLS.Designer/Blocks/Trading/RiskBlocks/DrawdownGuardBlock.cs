using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.Trading.RiskBlocks;

/// <summary>
/// Drawdown guard block that halts strategy execution when maximum drawdown is exceeded.
/// Emits a DENY <see cref="BlockSocketType.RiskDecision"/> when the drawdown limit is breached.
/// </summary>
public sealed class DrawdownGuardBlock : BlockBase
{
    private static readonly IReadOnlyList<IBlockSocket> _inputs =
    [
        BlockSocket.Input("signal_input", BlockSocketType.MLSignal),
    ];
    private static readonly IReadOnlyList<IBlockSocket> _outputs =
    [
        BlockSocket.Output("risk_output", BlockSocketType.RiskDecision),
    ];

    private float _peakEquity   = 1f;
    private float _equity       = 1f;
    private bool  _halted;

    private readonly BlockParameter<float> _maxDrawdownPctParam = new("MaxDrawdownPct", "Max Drawdown %", "Halt when drawdown exceeds this %", 10f, MinValue: 0.5f, MaxValue: 100f, IsOptimizable: true);
    private readonly BlockParameter<float> _recoveryPctParam    = new("RecoveryPct",    "Recovery %",     "Resume when equity recovers by this %", 2f, MinValue: 0.1f, MaxValue: 50f);

    /// <inheritdoc/>
    public override string BlockType   => "DrawdownGuardBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Drawdown Guard";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters => [_maxDrawdownPctParam, _recoveryPctParam];

    /// <summary>Initialises a new <see cref="DrawdownGuardBlock"/>.</summary>
    public DrawdownGuardBlock() : base(_inputs, _outputs) { }

    /// <inheritdoc/>
    public override void Reset()
    {
        _peakEquity = 1f;
        _equity     = 1f;
        _halted     = false;
    }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.MLSignal)
            return new ValueTask<BlockSignal?>(result: null);

        var drawdown = (_peakEquity - _equity) / _peakEquity * 100f;

        if (_halted)
        {
            // Recovery check: resume if equity climbed back by RecoveryPct
            if (drawdown < _maxDrawdownPctParam.DefaultValue - _recoveryPctParam.DefaultValue)
                _halted = false;
        }

        if (drawdown >= _maxDrawdownPctParam.DefaultValue)
            _halted = true;

        if (_halted)
        {
            var deny = new { allow = false, reason = "max_drawdown_exceeded", drawdown_pct = drawdown };
            return new ValueTask<BlockSignal?>(EmitObject(BlockId, "risk_output", BlockSocketType.RiskDecision, deny));
        }

        // Pass through
        var allow = new { allow = true, direction = ExtractDirection(signal.Value) };
        return new ValueTask<BlockSignal?>(EmitObject(BlockId, "risk_output", BlockSocketType.RiskDecision, allow));
    }

    private static string ExtractDirection(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("direction", out var d))
            return d.GetString() ?? "HOLD";
        return "HOLD";
    }
}
