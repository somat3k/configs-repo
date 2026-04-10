namespace MLS.Core.Constants;

public static partial class MessageTypes
{
    // ── Transactions domain ──────────────────────────────────────────────────────
    /// <summary>Submits an on-chain transaction for processing.</summary>
    public const string TxSubmit    = "TX_SUBMIT";
    /// <summary>On-chain transaction has been confirmed (mined).</summary>
    public const string TxConfirmed = "TX_CONFIRMED";
    /// <summary>On-chain transaction was reverted or failed.</summary>
    public const string TxReverted  = "TX_REVERTED";

    // ── Multi-Timeframe Classifier training ──────────────────────────────────────
    /// <summary>Triggers MTF Classifier ensemble training across multiple symbols and timeframes.</summary>
    public const string MTFTrainingJobStart    = "MTF_TRAINING_JOB_START";
    /// <summary>MTF Classifier ensemble training completed successfully.</summary>
    public const string MTFTrainingJobComplete = "MTF_TRAINING_JOB_COMPLETE";
}
