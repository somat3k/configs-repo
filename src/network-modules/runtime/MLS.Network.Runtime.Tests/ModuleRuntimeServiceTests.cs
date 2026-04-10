using Xunit;
using FluentAssertions;
using Moq;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging.Abstractions;
using MLS.Network.Runtime.Services;

namespace MLS.Network.Runtime.Tests;

public sealed class ModuleRuntimeServiceTests
{
    private static ModuleRuntimeService CreateService(IDockerClientFacade facade) =>
        new(facade, NullLogger<ModuleRuntimeService>.Instance);

    // ── tests against the real ModuleRuntimeService via a fake facade ──

    [Fact]
    public async Task GetStatusAsync_ReturnsNotFound_WhenNoContainersMatch()
    {
        var facade = new Mock<IDockerClientFacade>();
        facade.Setup(f => f.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<ContainerListResponse>());

        using var sut = CreateService(facade.Object);
        var status = await sut.GetStatusAsync("unknown-module", CancellationToken.None);

        status.State.Should().Be(ModuleState.NotFound);
        status.ContainerId.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsRunning_WhenContainerStateIsRunning()
    {
        var container = new ContainerListResponse
        {
            ID    = "abc123",
            State = "running",
            Image = "ghcr.io/somat3k/mls-trader:latest",
            Created = DateTime.UtcNow,
            Ports = new List<Port>(),
            Labels = new Dictionary<string, string>(),
            Names = new List<string> { "/mls-trader" },
        };

        var facade = new Mock<IDockerClientFacade>();
        facade.Setup(f => f.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new List<ContainerListResponse> { container });

        using var sut = CreateService(facade.Object);
        var status = await sut.GetStatusAsync("trader", CancellationToken.None);

        status.State.Should().Be(ModuleState.Running);
        status.ContainerId.Should().Be("abc123");
    }

    [Fact]
    public async Task ListModulesAsync_ReturnsEmptyList_WhenDockerReturnsNoContainers()
    {
        var facade = new Mock<IDockerClientFacade>();
        facade.Setup(f => f.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<ContainerListResponse>());

        using var sut = CreateService(facade.Object);
        var modules = await sut.ListModulesAsync(CancellationToken.None);

        modules.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNotFound_WhenDockerThrows()
    {
        var facade = new Mock<IDockerClientFacade>();
        facade.Setup(f => f.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new Exception("socket error"));

        using var sut = CreateService(facade.Object);
        var status = await sut.GetStatusAsync("trader", CancellationToken.None);

        status.State.Should().Be(ModuleState.NotFound);
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
