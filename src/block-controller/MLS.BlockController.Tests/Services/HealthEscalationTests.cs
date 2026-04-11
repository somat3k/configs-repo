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
/// Verifies <see cref="ModuleHealthTracker"/> health state transitions and broadcast events.
/// QA gates: BC-HEALTH-01 through BC-HEALTH-08.
/// </summary>
public sealed class HealthEscalationTests
{
    private readonly Mock<IMessageRouter> _routerMock = new();
    private readonly ModuleHealthTracker _tracker;

    public HealthEscalationTests()
    {
        _routerMock
            .Setup(r => r.BroadcastAsync(It.IsAny<EnvelopePayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tracker = new ModuleHealthTracker(
            _routerMock.Object,
            NullLogger<ModuleHealthTracker>.Instance);
    }

    // ── BC-HEALTH-01 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Initialize_SetsState_Initializing()
    {
        var moduleId = Guid.NewGuid();
        await _tracker.InitializeAsync(moduleId, "test-module");

        var state = await _tracker.GetHealthStateAsync(moduleId);
        state.Should().Be(ModuleHealthState.Initializing);
    }

    // ── BC-HEALTH-02 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task FirstHeartbeat_Transitions_Initializing_To_Healthy()
    {
        var moduleId = Guid.NewGuid();
        await _tracker.InitializeAsync(moduleId, "test-module");
        await _tracker.RecordHeartbeatAsync(moduleId);

        var state = await _tracker.GetHealthStateAsync(moduleId);
        state.Should().Be(ModuleHealthState.Healthy);
    }

    // ── BC-HEALTH-03 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task OneMissedHeartbeat_Transitions_Healthy_To_Degraded()
    {
        var moduleId = await SetupHealthyModuleAsync();

        await _tracker.RecordMissedHeartbeatAsync(moduleId);

        var state = await _tracker.GetHealthStateAsync(moduleId);
        state.Should().Be(ModuleHealthState.Degraded);
    }

    // ── BC-HEALTH-04 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MultipleMissedHeartbeats_Escalate_To_Unstable_Then_Offline()
    {
        var moduleId = await SetupHealthyModuleAsync();

        await _tracker.RecordMissedHeartbeatAsync(moduleId); // → Degraded
        await _tracker.RecordMissedHeartbeatAsync(moduleId); // → Unstable

        var afterTwo = await _tracker.GetHealthStateAsync(moduleId);
        afterTwo.Should().Be(ModuleHealthState.Unstable);

        await _tracker.RecordMissedHeartbeatAsync(moduleId); // → Offline

        var afterThree = await _tracker.GetHealthStateAsync(moduleId);
        afterThree.Should().Be(ModuleHealthState.Offline);
    }

    // ── BC-HEALTH-05 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Degradation_Broadcasts_MODULE_DEGRADED()
    {
        var moduleId = await SetupHealthyModuleAsync();

        _routerMock.Invocations.Clear(); // clear MODULE_RECOVERED from setup
        await _tracker.RecordMissedHeartbeatAsync(moduleId);

        _routerMock.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.ModuleDegraded),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── BC-HEALTH-06 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task OfflineTransition_Broadcasts_MODULE_OFFLINE()
    {
        var moduleId = await SetupHealthyModuleAsync();

        _routerMock.Invocations.Clear();
        await _tracker.RecordMissedHeartbeatAsync(moduleId); // Degraded
        await _tracker.RecordMissedHeartbeatAsync(moduleId); // Unstable
        await _tracker.RecordMissedHeartbeatAsync(moduleId); // Offline

        _routerMock.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.ModuleOffline),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── BC-HEALTH-07 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task OperatorDrain_Transitions_To_Draining_Immediately()
    {
        var moduleId = await SetupHealthyModuleAsync();

        await _tracker.TransitionStateAsync(moduleId, ModuleHealthState.Draining, "operator drain");

        var state = await _tracker.GetHealthStateAsync(moduleId);
        state.Should().Be(ModuleHealthState.Draining);
    }

    [Fact]
    public async Task DrainTransition_Broadcasts_MODULE_DRAINED()
    {
        var moduleId = await SetupHealthyModuleAsync();

        _routerMock.Invocations.Clear();
        await _tracker.TransitionStateAsync(moduleId, ModuleHealthState.Draining, "operator drain");

        _routerMock.Verify(
            r => r.BroadcastAsync(
                It.Is<EnvelopePayload>(e => e.Type == MessageTypes.ModuleDrained),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── BC-HEALTH-08 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReRegisterAfterOffline_ReturnsToInitializing()
    {
        var moduleId = Guid.NewGuid();
        await _tracker.InitializeAsync(moduleId, "re-registering-module");

        // Go offline
        await _tracker.TransitionStateAsync(moduleId, ModuleHealthState.Offline, "test");
        await _tracker.RemoveAsync(moduleId);

        // Re-register
        await _tracker.InitializeAsync(moduleId, "re-registering-module");

        var state = await _tracker.GetHealthStateAsync(moduleId);
        state.Should().Be(ModuleHealthState.Initializing);
    }

    // ── GetModulesInState ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetModulesInState_ReturnsCorrectSet()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        await _tracker.InitializeAsync(id1, "module-1");
        await _tracker.InitializeAsync(id2, "module-2");
        await _tracker.InitializeAsync(id3, "module-3");

        await _tracker.RecordHeartbeatAsync(id1); // Healthy
        await _tracker.RecordHeartbeatAsync(id2); // Healthy
        // id3 stays Initializing

        var healthy = await _tracker.GetModulesInStateAsync(ModuleHealthState.Healthy);
        var initializing = await _tracker.GetModulesInStateAsync(ModuleHealthState.Initializing);

        healthy.Should().Contain([id1, id2]);
        initializing.Should().Contain(id3);
    }

    // ── Recovery ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task HeartbeatAfterDegraded_Recovers_To_Healthy()
    {
        var moduleId = await SetupHealthyModuleAsync();

        await _tracker.RecordMissedHeartbeatAsync(moduleId); // Degraded
        await _tracker.RecordHeartbeatAsync(moduleId);       // Recovery

        var state = await _tracker.GetHealthStateAsync(moduleId);
        state.Should().Be(ModuleHealthState.Healthy);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<Guid> SetupHealthyModuleAsync()
    {
        var moduleId = Guid.NewGuid();
        await _tracker.InitializeAsync(moduleId, $"module-{moduleId:N}");
        await _tracker.RecordHeartbeatAsync(moduleId); // Initializing → Healthy
        return moduleId;
    }
}
