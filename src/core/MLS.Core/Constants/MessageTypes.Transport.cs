namespace MLS.Core.Constants;

public static partial class MessageTypes
{
    // ── Transport governance ─────────────────────────────────────────────────────

    /// <summary>
    /// Emitted when an incoming message is rejected at the admission gate.
    /// Payload: envelope metadata + rejection reason.
    /// </summary>
    public const string TransportAdmissionRejected = "TRANSPORT_ADMISSION_REJECTED";

    /// <summary>
    /// Emitted when a message cannot be delivered because the transport classes
    /// of sender and receiver are incompatible.
    /// </summary>
    public const string TransportCompatibilityError = "TRANSPORT_COMPATIBILITY_ERROR";

    /// <summary>
    /// Emitted when the declared <c>payload_schema</c> version is not supported
    /// by the target module.
    /// </summary>
    public const string SchemaVersionMismatch = "SCHEMA_VERSION_MISMATCH";

    /// <summary>
    /// Emitted when a streaming Class B lane detects backpressure and begins
    /// dropping events.
    /// </summary>
    public const string StreamBackpressureEvent = "STREAM_BACKPRESSURE_EVENT";

    /// <summary>
    /// Emitted by the receiver when an incoming message matches a previously
    /// seen dedupe token within the deduplication window.
    /// </summary>
    public const string DuplicateRequestDetected = "DUPLICATE_REQUEST_DETECTED";

    /// <summary>
    /// Emitted when a non-retriable operation fails and an operator must intervene
    /// before any retry is attempted.
    /// </summary>
    public const string OperatorActionRequired = "OPERATOR_ACTION_REQUIRED";

    /// <summary>
    /// Emitted when the payload body exceeds the declared maximum size threshold
    /// for the transport class.
    /// </summary>
    public const string PayloadSizeExceeded = "PAYLOAD_SIZE_EXCEEDED";

    /// <summary>
    /// Emitted when an artifact reference cannot be located in the declared storage.
    /// </summary>
    public const string ArtifactNotFound = "ARTIFACT_NOT_FOUND";

    /// <summary>
    /// Emitted when an artifact retrieval operation times out.
    /// </summary>
    public const string ArtifactRetrievalTimeout = "ARTIFACT_RETRIEVAL_TIMEOUT";

    /// <summary>
    /// Emitted when a payload integrity check fails (hash mismatch).
    /// </summary>
    public const string IntegrityCheckFailed = "INTEGRITY_CHECK_FAILED";

    /// <summary>
    /// Emitted when a new WebSocket / SignalR stream connection is established.
    /// </summary>
    public const string StreamConnected = "STREAM_CONNECTED";

    /// <summary>
    /// Emitted when a WebSocket / SignalR stream connection closes.
    /// </summary>
    public const string StreamDisconnected = "STREAM_DISCONNECTED";

    /// <summary>
    /// Emitted when a stream connection has been idle beyond the declared threshold.
    /// </summary>
    public const string StreamIdleTimeout = "STREAM_IDLE_TIMEOUT";

    /// <summary>
    /// Emitted when a subscriber reconnects and the stream resumes.
    /// </summary>
    public const string StreamResumed = "STREAM_RESUMED";
}
