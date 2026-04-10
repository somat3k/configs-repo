namespace MLS.WebApp.Components.Designer;

public partial class PropertyEditor
{
    private const string DefaultIndicatorCode = @"using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.UserBlock;

/// <summary>Custom indicator block — edit the computation logic below.</summary>
public sealed class MyIndicatorBlock : BlockBase
{
    public override string BlockType   => ""MyIndicatorBlock"";
    public override string DisplayName => ""My Indicator"";
    public override IReadOnlyList<BlockParameter> Parameters => [];

    public MyIndicatorBlock() : base(
        [BlockSocket.Input(""input"", BlockSocketType.IndicatorValue)],
        [BlockSocket.Output(""output"", BlockSocketType.IndicatorValue)]) { }

    public override void Reset() { }

    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.IndicatorValue)
            return new ValueTask<BlockSignal?>(result: null);

        // ── Your computation logic here ────────────────────────────────
        var value = signal.Value.GetSingle();
        var result = value; // passthrough — replace with your formula

        return new ValueTask<BlockSignal?>(
            EmitFloat(BlockId, ""output"", BlockSocketType.IndicatorValue, result));
    }
}";
}
