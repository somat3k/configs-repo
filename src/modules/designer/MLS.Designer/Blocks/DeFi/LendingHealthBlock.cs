using System.Text.Json;
using MLS.Core.Designer;
using MLS.Designer.Blocks;

namespace MLS.Designer.Blocks.DeFi;

/// <summary>
/// Lending health block — tracks the real-time health of an open lending position by
/// computing and emitting a composite health score across four dimensions:
/// <list type="bullet">
///   <item><b>LTV Ratio</b> — current loan-to-value vs. the liquidation LTV threshold.</item>
///   <item><b>Health Factor</b> — collateral value × liquidation LTV / outstanding debt.</item>
///   <item><b>Liquidation Distance</b> — percentage drop in collateral price that would trigger
///         liquidation.</item>
///   <item><b>Borrow Rate Pressure</b> — current utilisation-driven borrow APR as a fraction of
///         the configured maximum acceptable rate.</item>
/// </list>
/// </summary>
/// <remarks>
/// Input:  <see cref="BlockSocketType.DeFiSignal"/> carrying position updates. <br/>
/// Output: <see cref="BlockSocketType.HealthFactorUpdate"/> with a structured health report
///         that downstream blocks (evaluation, risk, liquidation guard) can inspect. <br/>
/// <para>
/// Severity levels emitted:
/// <c>Healthy</c> → <c>Warning</c> → <c>Critical</c> → <c>Liquidatable</c>.
/// A <see cref="BlockSocketType.HealthFactorUpdate"/> signal is always emitted on each
/// input so that connected loop blocks (e.g. kaizen-style evaluation tiles) receive a
/// continuous health stream to act upon.
/// </para>
/// </remarks>
public sealed class LendingHealthBlock : BlockBase
{
    // ── Thresholds ────────────────────────────────────────────────────────────────

    private readonly BlockParameter<decimal> _warningHfParam =
        new("WarningHealthFactor",   "Warning Health Factor",
            "Health factor below which a Warning is emitted",
            1.5m, MinValue: 1.01m, MaxValue: 5m);

    private readonly BlockParameter<decimal> _criticalHfParam =
        new("CriticalHealthFactor",  "Critical Health Factor",
            "Health factor below which a Critical alert is emitted",
            1.15m, MinValue: 1.01m, MaxValue: 3m);

    private readonly BlockParameter<decimal> _liquidatableHfParam =
        new("LiquidatableHealthFactor", "Liquidatable Health Factor",
            "Health factor at or below which the position can be liquidated",
            1.0m, MinValue: 0.5m, MaxValue: 1.1m);

    private readonly BlockParameter<decimal> _maxBorrowRateParam =
        new("MaxBorrowRateApr",      "Max Borrow Rate APR",
            "Maximum acceptable borrow APR (0–1 fractional, e.g. 0.15 = 15 %)",
            0.15m, MinValue: 0.001m, MaxValue: 1m);

    private readonly BlockParameter<decimal> _warningLtvParam =
        new("WarningLtvRatio",       "Warning LTV Ratio",
            "LTV ratio above which a Warning is emitted (0–1 fractional)",
            0.70m, MinValue: 0.10m, MaxValue: 0.99m);

    // ── State ─────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override string BlockType   => "LendingHealthBlock";
    /// <inheritdoc/>
    public override string DisplayName => "Lending Health";
    /// <inheritdoc/>
    public override IReadOnlyList<BlockParameter> Parameters =>
        [_warningHfParam, _criticalHfParam, _liquidatableHfParam,
         _maxBorrowRateParam, _warningLtvParam];

    /// <summary>Initialises a new <see cref="LendingHealthBlock"/>.</summary>
    public LendingHealthBlock() : base(
        [BlockSocket.Input("position_input", BlockSocketType.DeFiSignal)],
        [BlockSocket.Output("health_report", BlockSocketType.HealthFactorUpdate)]) { }

    /// <inheritdoc/>
    public override void Reset() { }

    /// <inheritdoc/>
    protected override ValueTask<BlockSignal?> ProcessCoreAsync(BlockSignal signal, CancellationToken ct)
    {
        if (signal.SocketType != BlockSocketType.DeFiSignal)
            return new ValueTask<BlockSignal?>(result: null);

        if (!TryExtractPosition(signal.Value,
                out var hf, out var ltvRatio, out var collateralValueUsd,
                out var debtValueUsd, out var borrowRateApr,
                out var protocol, out var collateralAsset, out var borrowAsset))
            return new ValueTask<BlockSignal?>(result: null);

        // ── Composite score (0–100, higher = healthier) ──────────────────────────
        decimal hfScore        = ComputeHfScore(hf);
        decimal ltvScore       = ComputeLtvScore(ltvRatio);
        decimal borrowRateScore = ComputeBorrowRateScore(borrowRateApr);
        decimal compositeScore = (hfScore * 0.5m + ltvScore * 0.3m + borrowRateScore * 0.2m);

        // ── Liquidation distance: % collateral drop to reach HF = 1.0 ────────────
        decimal liquidationDistancePct = hf > 1m
            ? Math.Round((1m - 1m / hf) * 100m, 2)
            : 0m;

        // ── Severity ──────────────────────────────────────────────────────────────
        var severity = hf <= _liquidatableHfParam.DefaultValue ? "Liquidatable"
                     : hf <= _criticalHfParam.DefaultValue     ? "Critical"
                     : hf <= _warningHfParam.DefaultValue       ? "Warning"
                     : ltvRatio >= _warningLtvParam.DefaultValue ? "Warning"
                     : "Healthy";

        var report = new
        {
            protocol,
            collateral_asset          = collateralAsset,
            borrow_asset              = borrowAsset,
            health_factor             = hf,
            ltv_ratio                 = ltvRatio,
            collateral_value_usd      = collateralValueUsd,
            debt_value_usd            = debtValueUsd,
            borrow_rate_apr           = borrowRateApr,
            liquidation_distance_pct  = liquidationDistancePct,
            composite_health_score    = compositeScore,
            severity,
            warning_health_factor     = _warningHfParam.DefaultValue,
            critical_health_factor    = _criticalHfParam.DefaultValue,
            max_borrow_rate_apr       = _maxBorrowRateParam.DefaultValue,
            timestamp                 = DateTimeOffset.UtcNow,
        };

        return new ValueTask<BlockSignal?>(
            EmitObject(BlockId, "health_report", BlockSocketType.HealthFactorUpdate, report));
    }

    // ── Score functions (0–100 scale; 100 = best possible) ────────────────────────

    private static decimal ComputeHfScore(decimal hf)
    {
        // HF 1.0 → score 0; HF 2.0 → score ~67; HF 5.0 → score ~100
        if (hf <= 1m) return 0m;
        return Math.Min(100m, (hf - 1m) / 4m * 100m);
    }

    private static decimal ComputeLtvScore(decimal ltvRatio)
    {
        // LTV 0.0 → score 100; LTV 0.9 → score 10; LTV ≥ 1.0 → score 0
        if (ltvRatio <= 0m) return 100m;
        if (ltvRatio >= 1m) return 0m;
        return Math.Round((1m - ltvRatio) * 100m, 2);
    }

    private decimal ComputeBorrowRateScore(decimal borrowRateApr)
    {
        var maxRate = _maxBorrowRateParam.DefaultValue;
        if (maxRate <= 0m || borrowRateApr <= 0m) return 100m;
        if (borrowRateApr >= maxRate) return 0m;
        return Math.Round((1m - borrowRateApr / maxRate) * 100m, 2);
    }

    // ── Extraction ────────────────────────────────────────────────────────────────

    private static bool TryExtractPosition(
        JsonElement value,
        out decimal hf,
        out decimal ltvRatio,
        out decimal collateralValueUsd,
        out decimal debtValueUsd,
        out decimal borrowRateApr,
        out string  protocol,
        out string  collateralAsset,
        out string  borrowAsset)
    {
        hf                 = decimal.MaxValue;
        ltvRatio           = 0m;
        collateralValueUsd = 0m;
        debtValueUsd       = 0m;
        borrowRateApr      = 0m;
        protocol           = string.Empty;
        collateralAsset    = string.Empty;
        borrowAsset        = string.Empty;

        if (value.ValueKind != JsonValueKind.Object) return false;

        // Health factor — required
        if (!value.TryGetProperty("health_factor", out var hfEl) || !hfEl.TryGetDecimal(out hf))
            return false;

        if (value.TryGetProperty("ltv_ratio",            out var ltv))   ltv.TryGetDecimal(out ltvRatio);
        if (value.TryGetProperty("collateral_value_usd", out var cv))    cv.TryGetDecimal(out collateralValueUsd);
        if (value.TryGetProperty("debt_value_usd",       out var dv))    dv.TryGetDecimal(out debtValueUsd);
        if (value.TryGetProperty("borrow_rate_apr",      out var br))    br.TryGetDecimal(out borrowRateApr);
        if (value.TryGetProperty("protocol",             out var pr))    protocol        = pr.GetString() ?? string.Empty;
        if (value.TryGetProperty("collateral_asset",     out var ca))    collateralAsset = ca.GetString() ?? string.Empty;
        if (value.TryGetProperty("borrow_asset",         out var ba))    borrowAsset     = ba.GetString() ?? string.Empty;

        return true;
    }
}
