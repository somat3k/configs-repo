namespace MLS.Core.Constants;

public static partial class MessageTypes
{
    // ── Hot-reload lifecycle (Section 8 — BCG-MASTER-SESSION-SCHEDULE) ───────────
    /// <summary>Module → Block Controller: new artifact staged and ready for drain/warm-up sequence.</summary>
    public const string HotReloadStaged    = "HOT_RELOAD_STAGED";
    /// <summary>Block Controller → module: stop accepting new work; drain in-flight requests.</summary>
    public const string DrainRequest       = "DRAIN_REQUEST";
    /// <summary>Module → Block Controller: drain complete; safe to cut over.</summary>
    public const string DrainComplete      = "DRAIN_COMPLETE";
    /// <summary>New instance → Block Controller: warm-up health checks passed; ready to receive traffic.</summary>
    public const string WarmUpReady        = "WARM_UP_READY";
    /// <summary>Block Controller → broadcast: routing table switched; new instance is now ACTIVE.</summary>
    public const string HotReloadComplete  = "HOT_RELOAD_COMPLETE";
    /// <summary>Block Controller → broadcast: new instance sustained health for 60 s post cut-over.</summary>
    public const string ReloadVerified     = "RELOAD_VERIFIED";
    /// <summary>Block Controller → broadcast: automatic rollback triggered; includes root-cause payload.</summary>
    public const string RollbackTriggered  = "ROLLBACK_TRIGGERED";

    // ── Runtime configuration push ───────────────────────────────────────────────
    /// <summary>
    /// Block Controller → module: push a live configuration value or feature-schema version change.
    /// Payload carries a discriminated <c>config_key</c> field (e.g. <c>feature_schema_version</c>).
    /// Modules apply the change without restart.
    /// </summary>
    public const string ConfigUpdate       = "CONFIG_UPDATE";

    // ── Model hot-swap ───────────────────────────────────────────────────────────
    /// <summary>Block Controller → ml-runtime: atomically swap the active ONNX model in ModelRegistry.</summary>
    public const string ModelReload        = "MODEL_RELOAD";
    /// <summary>Block Controller → ml-runtime: reload a Python training script from its IPFS CID after training jobs drain.</summary>
    public const string MlScriptReload     = "ML_SCRIPT_RELOAD";
}
