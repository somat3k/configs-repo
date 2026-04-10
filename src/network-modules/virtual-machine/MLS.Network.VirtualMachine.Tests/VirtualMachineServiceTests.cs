using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MLS.Network.VirtualMachine.Services;

namespace MLS.Network.VirtualMachine.Tests;

public sealed class VirtualMachineServiceTests
{
    private readonly VirtualMachineService _sut = new(NullLogger<VirtualMachineService>.Instance);

    [Fact]
    public async Task ExecuteAsync_SuccessfulScript_ReturnsSuccess()
    {
        var result = await _sut.ExecuteAsync(
            new SandboxRequest("1 + 1"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidScript_ReturnsFailure()
    {
        var result = await _sut.ExecuteAsync(
            new SandboxRequest("throw new System.Exception(\"test error\");"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(1);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSandboxAsync_ReturnsSandboxAfterExecution()
    {
        var result = await _sut.ExecuteAsync(
            new SandboxRequest("2 * 2"), CancellationToken.None);

        var sandbox = await _sut.GetSandboxAsync(result.SandboxId, CancellationToken.None);

        sandbox.Should().NotBeNull();
        sandbox!.SandboxId.Should().Be(result.SandboxId);
        sandbox.State.Should().Be(SandboxState.Completed);
    }

    [Fact]
    public async Task TerminateSandboxAsync_RemovesSandbox()
    {
        var result = await _sut.ExecuteAsync(
            new SandboxRequest("42"), CancellationToken.None);

        await _sut.TerminateSandboxAsync(result.SandboxId, CancellationToken.None);

        var sandbox = await _sut.GetSandboxAsync(result.SandboxId, CancellationToken.None);
        sandbox.Should().BeNull();
    }
}
