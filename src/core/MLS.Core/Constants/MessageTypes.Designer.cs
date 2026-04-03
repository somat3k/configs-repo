namespace MLS.Core.Constants;

public static partial class MessageTypes
{
    // ── Strategy lifecycle ───────────────────────────────────────────────────────
    /// <summary>Deploy a strategy graph — block-controller updates subscription table.</summary>
    public const string StrategyDeploy       = "STRATEGY_DEPLOY";
    /// <summary>Strategy state transition: Running / Stopped / Backtesting.</summary>
    public const string StrategyStateChange  = "STRATEGY_STATE_CHANGE";

    // ── Block graph runtime ──────────────────────────────────────────────────────
    /// <summary>Data signal propagating between connected blocks.</summary>
    public const string BlockSignal          = "BLOCK_SIGNAL";

    // ── ML training ─────────────────────────────────────────────────────────────
    /// <summary>Designer → Shell VM: kick off Python model training.</summary>
    public const string TrainingJobStart     = "TRAINING_JOB_START";
    /// <summary>Shell VM → Designer: per-epoch progress stream.</summary>
    public const string TrainingJobProgress  = "TRAINING_JOB_PROGRESS";
    /// <summary>Shell VM → Designer + ml-runtime: training finished, ONNX artefact ready.</summary>
    public const string TrainingJobComplete  = "TRAINING_JOB_COMPLETE";

    // ── AI Hub ──────────────────────────────────────────────────────────────────
    /// <summary>web-app → ai-hub: user message with assembled platform context.</summary>
    public const string AiQuery             = "AI_QUERY";
    /// <summary>ai-hub → web-app: streaming response chunk.</summary>
    public const string AiResponseChunk     = "AI_RESPONSE_CHUNK";
    /// <summary>ai-hub → web-app: canvas panel or chart action dispatch.</summary>
    public const string AiCanvasAction      = "AI_CANVAS_ACTION";

    // ── Hydra data collection ────────────────────────────────────────────────────
    /// <summary>Designer → data-layer: start a Hydra feed collection job.</summary>
    public const string DataCollectionStart = "DATA_COLLECTION_START";
    /// <summary>data-layer → designer / web-app: data gap detected in feed.</summary>
    public const string DataGapDetected     = "DATA_GAP_DETECTED";
    /// <summary>data-layer → designer / web-app: backfill complete, gap filled.</summary>
    public const string DataGapFilled       = "DATA_GAP_FILLED";

    // ── Arbitrage ────────────────────────────────────────────────────────────────
    /// <summary>Exchange adapter → arbitrage scanner: per-exchange price tick.</summary>
    public const string ExchangePriceUpdate = "EXCHANGE_PRICE_UPDATE";
    /// <summary>Arbitrage scanner → designer + broker: nHOP profitable path found.</summary>
    public const string ArbPathFound        = "ARB_PATH_FOUND";

    // ── DeFi health ──────────────────────────────────────────────────────────────
    /// <summary>defi → designer + ai-hub: health factor alert for an open position.</summary>
    public const string DefiHealthWarning   = "DEFI_HEALTH_WARNING";

    // ── Canvas persistence ───────────────────────────────────────────────────────
    /// <summary>web-app → block-controller: save MDI canvas layout.</summary>
    public const string CanvasLayoutSave    = "CANVAS_LAYOUT_SAVE";
}
