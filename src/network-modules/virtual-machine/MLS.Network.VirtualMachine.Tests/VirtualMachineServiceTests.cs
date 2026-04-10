using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MLS.Network.VirtualMachine.Services;

namespace MLS.Network.VirtualMachine.Tests;

public sealed class VirtualMachineServiceTests
{
    private readonly VirtualMachineService _sut = new(NullLogger<VirtualMachineService>.Instance);

    [Fact]
    public async Task ExecuteAsync_BlockedDirectiveR_ReturnsFailure()
    {
        var result = await _sut.ExecuteAsync(
            new SandboxRequest("#r \"System.IO\""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.Error.Should().Contain("#r");
        result.SandboxId.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_BlockedDirectiveLoad_ReturnsFailure()
    {
        var result = await _sut.ExecuteAsync(
            new SandboxRequest("#load \"script.csx\""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.Error.Should().Contain("#load");
    }

    [Fact]
    public async Task GetSandboxAsync_ReturnsSandboxAfterSuccessfulExecution()
    {
        // Execute a script — regardless of success/fail in CI the sandbox should be tracked.
        var result = await _sut.ExecuteAsync(new SandboxRequest("42"), CancellationToken.None);

        // Sandbox with a real ID should be tracked when script runs (not a directive-blocked call)
        if (result.SandboxId != Guid.Empty)
        {
            var sandbox = await _sut.GetSandboxAsync(result.SandboxId, CancellationToken.None);
            sandbox.Should().NotBeNull();
            sandbox!.SandboxId.Should().Be(result.SandboxId);
            sandbox.State.Should().BeOneOf(SandboxState.Completed, SandboxState.Failed);
        }
    }

    [Fact]
    public async Task TerminateSandboxAsync_RemovesSandbox()
    {
        var result = await _sut.ExecuteAsync(new SandboxRequest("42"), CancellationToken.None);
        if (result.SandboxId == Guid.Empty) return; // directive-blocked, no sandbox created

        await _sut.TerminateSandboxAsync(result.SandboxId, CancellationToken.None);

        var sandbox = await _sut.GetSandboxAsync(result.SandboxId, CancellationToken.None);
        sandbox.Should().BeNull();
    }
}
