using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MLS.Network.ContainerRegistry.Services;

namespace MLS.Network.ContainerRegistry.Tests;

public sealed class ContainerRegistryServiceTests
{
    private readonly ContainerRegistryService _sut = new(NullLogger<ContainerRegistryService>.Instance);

    [Fact]
    public async Task RegisterImageAsync_ReturnsRegisteredImage()
    {
        var image = await _sut.RegisterImageAsync(
            new RegisterImageRequest("mls-trader", "latest", "ghcr.io/somat3k", null),
            CancellationToken.None);

        image.Should().NotBeNull();
        image.Name.Should().Be("mls-trader");
        image.Tag.Should().Be("latest");
        image.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetImageAsync_ReturnsRegisteredImage()
    {
        var registered = await _sut.RegisterImageAsync(
            new RegisterImageRequest("mls-arbitrager", "v1.0", "ghcr.io/somat3k", null),
            CancellationToken.None);

        var retrieved = await _sut.GetImageAsync(registered.Id, CancellationToken.None);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(registered.Id);
    }

    [Fact]
    public async Task ListImagesAsync_ReturnsAllImages()
    {
        await _sut.RegisterImageAsync(
            new RegisterImageRequest("img-a", "latest", "registry.io", null), CancellationToken.None);
        await _sut.RegisterImageAsync(
            new RegisterImageRequest("img-b", "latest", "registry.io", null), CancellationToken.None);

        var images = new List<ContainerImage>();
        await foreach (var img in _sut.ListImagesAsync(CancellationToken.None))
            images.Add(img);

        images.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RecordHealthCheckAsync_UpdatesImageHealthStatus()
    {
        var image = await _sut.RegisterImageAsync(
            new RegisterImageRequest("mls-defi", "latest", "ghcr.io/somat3k", null),
            CancellationToken.None);

        var check = new HealthCheckResult(image.Id, DateTimeOffset.UtcNow, true, 200, "OK");
        await _sut.RecordHealthCheckAsync(image.Id, check, CancellationToken.None);

        var updated = await _sut.GetImageAsync(image.Id, CancellationToken.None);
        updated!.IsHealthy.Should().BeTrue();
        updated.LastHealthAt.Should().NotBeNull();
    }
}
