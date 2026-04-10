using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MLS.Network.NetworkMask.Models;
using MLS.Network.NetworkMask.Services;

namespace MLS.Network.NetworkMask.Tests;

public sealed class NetworkMaskServiceTests
{
    private static NetworkMaskService CreateService(NetworkMaskConfig? config = null)
    {
        var cfg = config ?? new NetworkMaskConfig();
        return new NetworkMaskService(
            Options.Create(cfg),
            NullLogger<NetworkMaskService>.Instance);
    }

    [Fact]
    public async Task RegisterEndpointAsync_StoresEndpoint()
    {
        var sut = CreateService();
        var reg = new EndpointRegistration("trader", "production",
            "http://trader:5300", "ws://trader:6300", ["trading"]);

        await sut.RegisterEndpointAsync(reg, CancellationToken.None);
        var info = await sut.ResolveEndpointAsync("trader", "production", CancellationToken.None);

        info.Should().NotBeNull();
        info!.HttpUrl.Should().Be("http://trader:5300");
    }

    [Fact]
    public async Task ResolveEndpointAsync_ReturnsNullForUnknown()
    {
        var sut  = CreateService();
        var info = await sut.ResolveEndpointAsync("unknown-module", "production", CancellationToken.None);

        info.Should().BeNull();
    }

    [Fact]
    public async Task ListEndpointsAsync_ReturnsAllRegistered()
    {
        var sut = CreateService();
        await sut.RegisterEndpointAsync(
            new EndpointRegistration("m1", "prod", "http://m1:1000", "ws://m1:2000", []),
            CancellationToken.None);
        await sut.RegisterEndpointAsync(
            new EndpointRegistration("m2", "prod", "http://m2:1001", "ws://m2:2001", []),
            CancellationToken.None);

        var endpoints = new List<EndpointInfo>();
        await foreach (var ep in sut.ListEndpointsAsync(CancellationToken.None))
            endpoints.Add(ep);

        endpoints.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RemoveEndpointAsync_RemovesEndpoint()
    {
        var sut = CreateService();
        await sut.RegisterEndpointAsync(
            new EndpointRegistration("broker", "production", "http://broker:5800", "ws://broker:6800", []),
            CancellationToken.None);

        var removed = await sut.RemoveEndpointAsync("broker", "production", CancellationToken.None);
        var info    = await sut.ResolveEndpointAsync("broker", "production", CancellationToken.None);

        removed.Should().BeTrue();
        info.Should().BeNull();
    }
}
