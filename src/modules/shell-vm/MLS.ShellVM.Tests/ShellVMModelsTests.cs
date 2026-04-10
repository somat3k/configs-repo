using FluentAssertions;
using MLS.ShellVM.Constants;
using MLS.ShellVM.Models;
using MLS.ShellVM.Payloads;
using MLS.Core.Contracts;
using MLS.Core.Constants;
using Xunit;

namespace MLS.ShellVM.Tests;

/// <summary>
/// Tests for constants, payload records, model state machine, and envelope creation.
/// </summary>
public sealed class ShellVMModelsTests
{
    // ── ShellVMNetworkConstants ───────────────────────────────────────────────

    [Fact]
    public void NetworkConstants_HaveCorrectPorts()
    {
        ShellVMNetworkConstants.HttpPort.Should().Be(5950);
        ShellVMNetworkConstants.WsPort.Should().Be(6950);
        ShellVMNetworkConstants.ModuleName.Should().Be("shell-vm");
    }

    // ── ShellVMMessageTypes ───────────────────────────────────────────────────

    [Fact]
    public void MessageTypes_MatchCoreConstants()
    {
        ShellVMMessageTypes.ExecRequest.Should().Be(MessageTypes.ShellExecRequest);
        ShellVMMessageTypes.Input.Should().Be(MessageTypes.ShellInput);
        ShellVMMessageTypes.Resize.Should().Be(MessageTypes.ShellResize);
        ShellVMMessageTypes.Output.Should().Be(MessageTypes.ShellOutput);
        ShellVMMessageTypes.SessionState.Should().Be(MessageTypes.ShellSessionState);
        ShellVMMessageTypes.SessionCreated.Should().Be(MessageTypes.ShellSessionCreated);
        ShellVMMessageTypes.SessionTerminated.Should().Be(MessageTypes.ShellSessionTerminated);
    }

    // ── ExecutionBlock state machine ──────────────────────────────────────────

    [Fact]
    public void ExecutionBlock_DefaultState_IsCreated()
    {
        var block = new ExecutionBlock { Label = "test" };
        block.State.Should().Be(ExecutionBlockState.Created);
        block.Shell.Should().Be("/bin/sh");
        block.WorkingDirectory.Should().Be("/app");
        block.ExitCode.Should().BeNull();
    }

    [Fact]
    public void ExecutionBlock_CanTransitionThroughAllStates()
    {
        var block = new ExecutionBlock { Label = "state-machine-test" };

        block.State = ExecutionBlockState.Starting;
        block.State.Should().Be(ExecutionBlockState.Starting);

        block.State    = ExecutionBlockState.Running;
        block.StartedAt = DateTimeOffset.UtcNow;
        block.State.Should().Be(ExecutionBlockState.Running);

        block.State       = ExecutionBlockState.Completed;
        block.ExitCode    = 0;
        block.CompletedAt = DateTimeOffset.UtcNow;
        block.State.Should().Be(ExecutionBlockState.Completed);
    }

    [Fact]
    public void ExecutionBlock_CanReachErrorState()
    {
        var block = new ExecutionBlock { Label = "error-test" };
        block.State    = ExecutionBlockState.Running;
        block.State    = ExecutionBlockState.Error;
        block.ExitCode = 1;

        block.State.Should().Be(ExecutionBlockState.Error);
        block.ExitCode.Should().Be(1);
    }

    // ── OutputChunk ───────────────────────────────────────────────────────────

    [Fact]
    public void OutputChunk_PropertiesRoundtrip()
    {
        var sessionId = Guid.NewGuid();
        var ts        = DateTimeOffset.UtcNow;
        var chunk     = new OutputChunk(sessionId, OutputStream.Stdout, "hello\n", 42, ts);

        chunk.SessionId.Should().Be(sessionId);
        chunk.Stream.Should().Be(OutputStream.Stdout);
        chunk.Data.Should().Be("hello\n");
        chunk.Sequence.Should().Be(42);
        chunk.Timestamp.Should().Be(ts);
    }

    // ── Payload records ───────────────────────────────────────────────────────

    [Fact]
    public void ShellExecRequestPayload_DefaultsAreCorrect()
    {
        var payload = new ShellExecRequestPayload("echo hi", "/app", null);
        payload.TimeoutSeconds.Should().Be(300);
        payload.CaptureOutput.Should().BeTrue();
    }

    [Fact]
    public void ShellOutputPayload_RoundtripsViaEnvelope()
    {
        var outputPayload = new ShellOutputPayload(Guid.NewGuid().ToString(), "stdout", "hello", 1, DateTimeOffset.UtcNow.ToString("O"));
        var envelope      = EnvelopePayload.Create(
            MessageTypes.ShellOutput, "shell-vm", outputPayload);

        envelope.Type.Should().Be(MessageTypes.ShellOutput);
        envelope.ModuleId.Should().Be("shell-vm");
        envelope.Version.Should().Be(1);
        envelope.Payload.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public void ShellSessionStatePayload_RoundtripsViaEnvelope()
    {
        var statePayload = new ShellSessionStatePayload(
            PreviousState: "Running",
            CurrentState:  "Completed",
            ExitCode:      0,
            DurationMs:    4821);

        var envelope = EnvelopePayload.Create(
            MessageTypes.ShellSessionState, "shell-vm", statePayload);

        envelope.Type.Should().Be(MessageTypes.ShellSessionState);
    }

    // ── ShellVMConfig ─────────────────────────────────────────────────────────

    [Fact]
    public void ShellVMConfig_DefaultsAreReasonable()
    {
        var config = new ShellVMConfig();
        config.MaxConcurrentSessions.Should().Be(32);
        config.CommandTimeoutSeconds.Should().Be(600);
        config.AuditEnabled.Should().BeTrue();
        config.AllowedShells.Should().Contain("/bin/sh");
        config.AllowedShells.Should().Contain("python3");
    }

    // ── AuditEntry ────────────────────────────────────────────────────────────

    [Fact]
    public void AuditEntry_DefaultsAreCorrect()
    {
        var entry = new AuditEntry
        {
            BlockId = Guid.NewGuid(),
            Command = "ls",
        };

        entry.Id.Should().NotBe(Guid.Empty);
        entry.ExitCode.Should().BeNull();
        entry.DurationMs.Should().BeNull();
    }

    // ── PtyHandle ─────────────────────────────────────────────────────────────

    [Fact]
    public void PtyHandle_IsValueEquality()
    {
        var h1 = new PtyHandle(1234, "sh", 220, 50);
        var h2 = new PtyHandle(1234, "sh", 220, 50);
        h1.Should().Be(h2);
    }
}
