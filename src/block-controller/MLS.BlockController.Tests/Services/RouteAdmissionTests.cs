using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MLS.BlockController.Models;
using MLS.BlockController.Services;
using MLS.Core.Constants;
using MLS.Core.Contracts;
using Moq;
using Xunit;

namespace MLS.BlockController.Tests.Services;

/// <summary>
/// Verifies <see cref="RouteAdmissionService"/> gate sequence, score-based selection, and rejection events.
/// QA gates: BC-ROUTE-01 through BC-ROUTE-07.
/// </summary>
public sealed class RouteAdmissionTests
{
    private readonly Mock<IMessageRouter> _routerMock = new();
    private readonly InMemoryCapabilityRegistry _capabilities;
    private readonly ModuleHealthTracker _health;
    private readonly RouteAdmissionService _admission;

    public RouteAdmissionTests()
    {
        _routerMock
            .Setup(r => r.BroadcastAsync(It.IsAny<EnvelopePayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _capabilities = new InMemoryCapabilityRegistry(
            _routerMock.Object,
            NullLogger<InMemoryCapabilityRegistry>.Instance);

        _health = new ModuleHealthTracker(
            _routerMock.Object,
            NullLogger<ModuleHealthTracker>.Instance);

        _admission = new RouteAdmissionService(
            _capabilities,
            _health,
            _routerMock.Object,
            NullLogger<RouteAdmissionService>.Instance);
    }

    // ── BC-ROUTE-01 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthyModuleWithCapability_IsAdmitted()
    {
        var moduleId = await RegisterHealthyModuleAsync(["INFERENCE_REQUEST"]);

        var result = await _admission.AdmitAsync("INFERENCE_REQUEST", Guid.NewGuid());

        result.IsAdmitted.Should().BeTrue();
        result.TargetModuleId.Should().Be(moduleId);
        result.Score.Should().BePositive();
    }

    // ── BC-ROUTE-02 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DegradedModule_IsRejectedForStandardRouting()
    {
        var moduleId = await RegisterHealthyModuleAsync(["INFERENCE_REQUEST"]);
        await _health.RecordMissedHeartbeatAsync(moduleId); // → Degraded

        var result = await _admission.AdmitAsync("INFERENCE_REQUEST", Guid.NewGuid());

        result.IsAdmitted.Should().BeFalse();
        result.RejectionReason.Should().Be(RouteRejectionReasons.NoHealthyModule);
    }

    // ── BC-ROUTE-03 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DrainingModule_IsRejectedForNewWorkloads()
    {
        var moduleId = await RegisterHealthyModuleAsync(["INFERENCE_REQUEST"]);
        await _health.TransitionStateAsync(moduleId, ModuleHealthState.Draining, "operator drain");

        var result = await _admission.AdmitAsync("INFERENCE_REQUEST", Guid.NewGuid());

        result.IsAdmitted.Should().BeFalse();
        result.RejectionReason.Should().Be(RouteRejectionReasons.NoHealthyModule);
    }

    // ── BC-ROUTE-04 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task NoCapableModule_Rejected_WithCorrectReason()
    {
        var result = await _admission.AdmitAsync("INFERENCE_REQUEST", Guid.NewGuid());

        result.IsAdmitted.Should().BeFalse();
        result.RejectionReason.Should().Be(RouteRejectionReasons.NoCapableModule);
    }

    [Fact]
    public async Task NoCapableModule_Broadcasts_ROUTE_REJECTED()
    {
        _routerMock.Invocations.Clear();
        await _admission.AdmitAsync("INFERENCE_REQUEST", Guid.NewGuid());

        _routerMock.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.RouteRejected),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── BC-ROUTE-05 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CapableButUnhealthyModule_Rejected_WithCorrectReason()
    {
        var moduleId = await RegisterHealthyModuleAsync(["INFERENCE_REQUEST"]);
        // Put module offline
        await _health.RecordMissedHeartbeatAsync(moduleId); // Degraded
        await _health.RecordMissedHeartbeatAsync(moduleId); // Unstable
        await _health.RecordMissedHeartbeatAsync(moduleId); // Offline

        var result = await _admission.AdmitAsync("INFERENCE_REQUEST", Guid.NewGuid());

        result.IsAdmitted.Should().BeFalse();
        result.RejectionReason.Should().Be(RouteRejectionReasons.NoHealthyModule);
    }

    // ── BC-ROUTE-06 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleHealthyCandidates_SelectsHighestScore()
    {
        // Specialist: only INFERENCE_REQUEST → higher capability match score
        var specialist = await RegisterHealthyModuleAsync(["INFERENCE_REQUEST"]);

        // Generalist: many operations → lower score
        var generalist = await RegisterHealthyModuleAsync(
            ["INFERENCE_REQUEST", "TRADE_SIGNAL", "ORDER_CREATE", "SHELL_OUTPUT", "SHELL_INPUT"]);

        var result = await _admission.AdmitAsync("INFERENCE_REQUEST", Guid.NewGuid());

        result.IsAdmitted.Should().BeTrue();
        result.TargetModuleId.Should().Be(specialist);
    }

    // ── BC-ROUTE-07 ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AdmissionResult_ContainsCandidatesEvaluatedCount()
    {
        await RegisterHealthyModuleAsync(["INFERENCE_REQUEST"]);
        await RegisterHealthyModuleAsync(["INFERENCE_REQUEST"]);

        var result = await _admission.AdmitAsync("INFERENCE_REQUEST", Guid.NewGuid());

        result.IsAdmitted.Should().BeTrue();
        result.CandidatesEvaluated.Should().Be(2);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<Guid> RegisterHealthyModuleAsync(IReadOnlyList<string> ops)
    {
        var moduleId = Guid.NewGuid();
        var name = $"module-{moduleId:N}";

        var record = new CapabilityRecord(
            ModuleId: moduleId,
            ModuleName: name,
            OperationTypes: ops,
            TensorClassesIn: [],
            TensorClassesOut: [],
            TransportInterfaces: ["websocket"],
            BatchSupport: "none",
            StreamingSupport: "none",
            IsStateful: false,
            Version: "1.0.0",
            RegisteredAt: DateTimeOffset.UtcNow,
            LastUpdatedAt: DateTimeOffset.UtcNow);

        await _capabilities.RegisterAsync(record);
        await _health.InitializeAsync(moduleId, name);
        await _health.RecordHeartbeatAsync(moduleId); // Initializing → Healthy

        return moduleId;
    }
}
