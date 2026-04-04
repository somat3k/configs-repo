namespace MLS.Core.Constants;

/// <summary>
/// Named constants for all ML training lifecycle state strings used across Designer blocks,
/// Shell VM payloads, and the Python training pipeline stdout protocol.
/// </summary>
/// <remarks>
/// Always reference these constants instead of inline string literals so that the state
/// machine is refactor-safe and self-documenting.
/// </remarks>
public static class TrainingState
{
    // ── Job lifecycle ────────────────────────────────────────────────────────────

    /// <summary>Job dispatched; awaiting Shell VM acknowledgement.</summary>
    public const string Pending         = "PENDING";

    /// <summary>Training in progress — emitted once per epoch.</summary>
    public const string Training        = "TRAINING";

    /// <summary>Training loop finished; final metrics available.</summary>
    public const string Complete        = "COMPLETE";

    /// <summary>Training failed or was aborted by the pipeline.</summary>
    public const string Failed          = "FAILED";

    // ── Validation outcomes ───────────────────────────────────────────────────

    /// <summary>Model passed all validation thresholds.</summary>
    public const string Accepted        = "ACCEPTED";

    /// <summary>Model failed one or more validation thresholds.</summary>
    public const string Rejected        = "REJECTED";

    // ── Export lifecycle ──────────────────────────────────────────────────────

    /// <summary>ONNX + JOBLIB artefacts confirmed; IPFS CID available.</summary>
    public const string ExportReady     = "EXPORT_READY";

    // ── Hyperparameter search lifecycle ──────────────────────────────────────

    /// <summary>Search loop started; first trial dispatched.</summary>
    public const string SearchStarted   = "SEARCH_STARTED";

    /// <summary>Search in progress; intermediate best result available.</summary>
    public const string SearchRunning   = "SEARCH_RUNNING";

    /// <summary>All trials complete (or early-stop triggered); best result emitted.</summary>
    public const string SearchComplete  = "SEARCH_COMPLETE";
}
