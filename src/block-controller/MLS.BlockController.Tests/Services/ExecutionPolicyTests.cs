using FluentAssertions;
using MLS.BlockController.Services;
using MLS.Core.Constants;
using Xunit;

namespace MLS.BlockController.Tests.Services;

/// <summary>
/// Verifies <see cref="ExecutionPolicyService"/> lane assignment, timeout, retry budget,
/// and runtime mode admission rules.
/// QA gates: BC-POLICY-01 through BC-POLICY-05.
/// </summary>
public sealed class ExecutionPolicyTests
{
    private readonly ExecutionPolicyService _service = new();

    // ── BC-POLICY-01 ──────────────────────────────────────────────────────────

    [Fact]
    public void LaneA_Request_HasTimeout_Under10ms()
    {
        var policy = _service.GetPolicy(MessageTypes.ModuleHeartbeat);

        policy.Lane.Should().Be(ExecutionLane.A);
        policy.TimeoutMs.Should().NotBeNull();
        policy.TimeoutMs.Should().BeLessOrEqualTo(10);
    }

    [Theory]
    [InlineData(MessageTypes.ModuleRegister)]
    [InlineData(MessageTypes.ModuleHeartbeat)]
    [InlineData(MessageTypes.ModuleDeregister)]
    public void LaneA_ControlPlane_Messages_HaveNoRetries(string messageType)
    {
        var policy = _service.GetPolicy(messageType);

        policy.Lane.Should().Be(ExecutionLane.A);
        policy.MaxRetries.Should().Be(0);
    }

    // ── BC-POLICY-02 ──────────────────────────────────────────────────────────

    [Fact]
    public void LaneB_InferenceRequest_Gets1Retry()
    {
        var policy = _service.GetPolicy(MessageTypes.InferenceRequest);

        policy.Lane.Should().Be(ExecutionLane.B);
        policy.MaxRetries.Should().Be(1);
        policy.TimeoutMs.Should().Be(50);
    }

    [Theory]
    [InlineData(MessageTypes.TradeSignal)]
    [InlineData(MessageTypes.InferenceRequest)]
    [InlineData(MessageTypes.OrderCreate)]
    [InlineData(MessageTypes.ArbitrageOpportunity)]
    public void LaneB_Trading_Messages_HaveTimeout50ms(string messageType)
    {
        var policy = _service.GetPolicy(messageType);

        policy.Lane.Should().Be(ExecutionLane.B);
        policy.TimeoutMs.Should().Be(50);
    }

    // ── BC-POLICY-03 ──────────────────────────────────────────────────────────

    [Fact]
    public void LaneE_Request_HasNoTimeoutSLO()
    {
        var policy = _service.GetPolicy(MessageTypes.ShellExecRequest);

        policy.Lane.Should().Be(ExecutionLane.E);
        policy.TimeoutMs.Should().BeNull();
    }

    // ── BC-POLICY-04 ──────────────────────────────────────────────────────────

    [Fact]
    public void MaintenanceMode_Rejects_InferenceRequest()
    {
        var rejection = _service.EvaluateRuntimeMode(MessageTypes.InferenceRequest, "Maintenance");

        rejection.Should().Be(RouteRejectionReasons.PolicyDenied);
    }

    [Fact]
    public void MaintenanceMode_Rejects_OrderCreate()
    {
        var rejection = _service.EvaluateRuntimeMode(MessageTypes.OrderCreate, "Maintenance");

        rejection.Should().Be(RouteRejectionReasons.PolicyDenied);
    }

    [Fact]
    public void NormalMode_Admits_InferenceRequest()
    {
        var rejection = _service.EvaluateRuntimeMode(MessageTypes.InferenceRequest, "Normal");

        rejection.Should().BeNull();
    }

    [Fact]
    public void MaintenanceMode_Allows_Heartbeat()
    {
        // Heartbeats are control-plane and must not be blocked by maintenance mode
        var rejection = _service.EvaluateRuntimeMode(MessageTypes.ModuleHeartbeat, "Maintenance");

        rejection.Should().BeNull();
    }

    // ── Default policy ────────────────────────────────────────────────────────

    [Fact]
    public void UnknownMessageType_ReturnsDefaultLaneBPolicy()
    {
        var policy = _service.GetPolicy("UNKNOWN_CUSTOM_TYPE");

        policy.Lane.Should().Be(ExecutionLane.B);
        policy.TimeoutMs.Should().Be(50);
        policy.MaxRetries.Should().Be(1);
    }
}
