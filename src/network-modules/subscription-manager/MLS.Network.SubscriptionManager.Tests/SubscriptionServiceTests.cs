using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLS.Network.SubscriptionManager.Services;

namespace MLS.Network.SubscriptionManager.Tests;

public sealed class SubscriptionServiceTests
{
    private readonly Mock<IHubContext<Hubs.SubscriptionManagerHub>> _hubContextMock = new();
    private readonly SubscriptionService _sut;

    public SubscriptionServiceTests()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(Mock.Of<IHubClients>());
        _sut = new SubscriptionService(
            _hubContextMock.Object,
            NullLogger<SubscriptionService>.Instance);
    }

    [Fact]
    public async Task SubscribeAsync_ReturnsSubscriptionId()
    {
        var subId = await _sut.SubscribeAsync("market-data", "conn-1", CancellationToken.None);

        subId.Should().NotBeNullOrEmpty();
        subId.Should().HaveLength(32);
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesSubscription()
    {
        var subId = await _sut.SubscribeAsync("orders", "conn-2", CancellationToken.None);
        await _sut.UnsubscribeAsync(subId, CancellationToken.None);

        var remaining = new List<SubscriptionInfo>();
        await foreach (var s in _sut.GetSubscriptionsAsync("orders", CancellationToken.None))
            remaining.Add(s);

        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_ReturnsSubscriberCount()
    {
        await _sut.SubscribeAsync("signals", "conn-3", CancellationToken.None);
        await _sut.SubscribeAsync("signals", "conn-4", CancellationToken.None);

        // Hub delivery will fail gracefully (no real connections) — count is still returned
        var count = await _sut.PublishAsync("signals", "hello", CancellationToken.None);
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetTopics_ReturnsAllTopics()
    {
        await _sut.SubscribeAsync("topic-a", "conn-5", CancellationToken.None);
        await _sut.SubscribeAsync("topic-b", "conn-6", CancellationToken.None);

        var topics = _sut.GetTopics();
        topics.Should().Contain("topic-a");
        topics.Should().Contain("topic-b");
    }

    [Fact]
    public async Task UnsubscribeAllAsync_RemovesAllConnectionSubscriptions()
    {
        await _sut.SubscribeAsync("t1", "conn-7", CancellationToken.None);
        await _sut.SubscribeAsync("t2", "conn-7", CancellationToken.None);
        await _sut.UnsubscribeAllAsync("conn-7", CancellationToken.None);

        var t1 = new List<SubscriptionInfo>();
        await foreach (var s in _sut.GetSubscriptionsAsync("t1", CancellationToken.None))
            t1.Add(s);

        t1.Should().BeEmpty();
    }
}
