using MLS.BlockController.Models;

namespace MLS.BlockController.Services;

/// <summary>
/// Result of a route admission decision.
/// </summary>
/// <param name="IsAdmitted">True when a target module was selected.</param>
/// <param name="TargetModuleId">The selected module ID when <see cref="IsAdmitted"/> is true.</param>
/// <param name="Score">Composite routing score for the selected candidate.</param>
/// <param name="RejectionReason">Structured rejection reason code when <see cref="IsAdmitted"/> is false.</param>
/// <param name="CandidatesEvaluated">Total candidates evaluated.</param>
public sealed record RouteAdmissionResult(
    bool IsAdmitted,
    Guid? TargetModuleId,
    int Score,
    string? RejectionReason,
    int CandidatesEvaluated)
{
    /// <summary>Build a successful admission result.</summary>
    public static RouteAdmissionResult Admitted(Guid targetModuleId, int score, int candidatesEvaluated) =>
        new(IsAdmitted: true, TargetModuleId: targetModuleId, Score: score,
            RejectionReason: null, CandidatesEvaluated: candidatesEvaluated);

    /// <summary>Build a rejection result.</summary>
    public static RouteAdmissionResult Rejected(string reason, int candidatesEvaluated) =>
        new(IsAdmitted: false, TargetModuleId: null, Score: 0,
            RejectionReason: reason, CandidatesEvaluated: candidatesEvaluated);
}

/// <summary>
/// Evaluates whether an execution route request can be admitted and selects the best target module.
/// Implements the routing gate sequence defined in <c>docs/bcg/routing-policy-spec.md</c>.
/// </summary>
public interface IRouteAdmissionService
{
    /// <summary>
    /// Evaluate all routing gates for the given <paramref name="operationType"/> and return
    /// an admission result with the highest-scoring eligible module or a rejection reason.
    /// </summary>
    Task<RouteAdmissionResult> AdmitAsync(
        string operationType,
        Guid requestId,
        CancellationToken ct = default);
}
