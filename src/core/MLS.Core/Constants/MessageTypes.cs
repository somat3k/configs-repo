namespace MLS.Core.Constants;

/// <summary>
/// Canonical string constants for all inter-module envelope message types.
/// <para>
/// Usage: always reference these constants — never use raw string literals for message types.
/// </para>
/// </summary>
public static partial class MessageTypes
{
    // ── Module lifecycle ─────────────────────────────────────────────────────────
    /// <summary>Module registration on startup.</summary>
    public const string ModuleRegister = "MODULE_REGISTER";
    /// <summary>Periodic heartbeat to confirm liveness.</summary>
    public const string ModuleHeartbeat = "MODULE_HEARTBEAT";
    /// <summary>Graceful module deregistration.</summary>
    public const string ModuleDeregister = "MODULE_DEREGISTER";

    // ── Trading domain ───────────────────────────────────────────────────────────
    /// <summary>ML-generated trade signal (BUY / SELL / HOLD).</summary>
    public const string TradeSignal = "TRADE_SIGNAL";
    /// <summary>Detected cross-exchange arbitrage opportunity.</summary>
    public const string ArbitrageOpportunity = "ARBITRAGE_OPPORTUNITY";

    // ── ML inference ─────────────────────────────────────────────────────────────
    /// <summary>Inference request sent to <c>ml-runtime</c>.</summary>
    public const string InferenceRequest = "INFERENCE_REQUEST";
    /// <summary>Inference result returned by <c>ml-runtime</c>.</summary>
    public const string InferenceResult = "INFERENCE_RESULT";

    // ── Shell VM ─────────────────────────────────────────────────────────────────
    /// <summary>Execute a command in the Shell VM.</summary>
    public const string ShellExecRequest = "SHELL_EXEC_REQUEST";
    /// <summary>Raw stdin input to a shell session.</summary>
    public const string ShellInput = "SHELL_INPUT";
    /// <summary>Terminal resize event.</summary>
    public const string ShellResize = "SHELL_RESIZE";
    /// <summary>stdout / stderr output chunk from a shell session.</summary>
    public const string ShellOutput = "SHELL_OUTPUT";
    /// <summary>Shell session state transition notification.</summary>
    public const string ShellSessionState = "SHELL_SESSION_STATE";
    /// <summary>Shell session created confirmation.</summary>
    public const string ShellSessionCreated = "SHELL_SESSION_CREATED";
    /// <summary>Shell session terminated notification.</summary>
    public const string ShellSessionTerminated = "SHELL_SESSION_TERMINATED";

    // ── Broker / Order management ────────────────────────────────────────────────
    /// <summary>Trader or DeFi → broker: place a new order on a venue.</summary>
    public const string OrderCreate       = "ORDER_CREATE";
    /// <summary>Trader or DeFi → broker: cancel an open order by clientOrderId.</summary>
    public const string OrderCancel       = "ORDER_CANCEL";
    /// <summary>broker → requester: order accepted by the venue.</summary>
    public const string OrderConfirmation = "ORDER_CONFIRMATION";
    /// <summary>broker → all subscribers: order partially or fully filled.</summary>
    public const string FillNotification  = "FILL_NOTIFICATION";
    /// <summary>broker → all subscribers: current open position for a symbol.</summary>
    public const string PositionUpdate    = "POSITION_UPDATE";
}
