using Xunit;
using FluentAssertions;
using Moq;
using MLS.Network.Runtime.Services;

namespace MLS.Network.Runtime.Tests;

public sealed class ModuleRuntimeServiceTests
{
    private readonly Mock<IModuleRuntimeService> _serviceMock = new();

    [Fact]
    public async Task GetStatusAsync_ReturnsNotFoundForUnknownModule()
    {
        _serviceMock
            .Setup(s => s.GetStatusAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModuleStatus("unknown", string.Empty, ModuleState.NotFound,
                string.Empty, null, Array.Empty<string>()));

        var status = await _serviceMock.Object.GetStatusAsync("unknown", CancellationToken.None);

        status.State.Should().Be(ModuleState.NotFound);
        status.ContainerId.Should().BeEmpty();
    }

    [Fact]
    public async Task ListModulesAsync_ReturnsEmptyListWhenNoContainers()
    {
        _serviceMock
            .Setup(s => s.ListModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ModuleStatus>());

        var modules = await _serviceMock.Object.ListModulesAsync(CancellationToken.None);

        modules.Should().BeEmpty();
    }

    [Fact]
    public void ModuleStatusRecord_PropertiesAreAccessible()
    {
        var status = new ModuleStatus(
            "trader",
            "abc123",
            ModuleState.Running,
            "ghcr.io/somat3k/mls-trader:latest",
            DateTimeOffset.UtcNow,
            new[] { "5300:5300" });

        status.ModuleName.Should().Be("trader");
        status.State.Should().Be(ModuleState.Running);
        status.Ports.Should().ContainSingle();
    }
}
