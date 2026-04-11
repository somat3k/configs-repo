using System.Text.Json.Serialization;

namespace MLS.Core.Contracts.BlockController;

/// <summary>
/// Payload emitted when an execution route request is rejected by the admission service.
/// </summary>
/// <param name="RequestId">Correlation ID for the rejected request.</param>
/// <param name="WorkloadType">The envelope type of the rejected request (e.g. INFERENCE_REQUEST).</param>
/// <param name="Reason">Structured rejection reason code.</param>
/// <param name="CandidatesEvaluated">Number of modules evaluated as candidates.</param>
/// <param name="CandidatesAdmitted">Number of candidates that passed all gates (0 on rejection).</param>
/// <param name="RuntimeMode">The current runtime mode of the controller at decision time.</param>
/// <param name="Timestamp">UTC time of the rejection decision.</param>
public sealed record RouteRejectedPayload(
    [property: JsonPropertyName("request_id")] Guid RequestId,
    [property: JsonPropertyName("workload_type")] string WorkloadType,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("candidates_evaluated")] int CandidatesEvaluated,
    [property: JsonPropertyName("candidates_admitted")] int CandidatesAdmitted,
    [property: JsonPropertyName("runtime_mode")] string RuntimeMode,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
