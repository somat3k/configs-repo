using System.Threading.Channels;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MLS.ShellVM.Hubs;
using MLS.ShellVM.Models;
using MLS.ShellVM.Services;
using MLS.Core.Contracts;
using MLS.ShellVM.Constants;
using Xunit;

namespace MLS.ShellVM.Tests;

/// <summary>
/// Tests for <see cref="OutputBroadcaster"/> output streaming and subscription management.
/// </summary>
public sealed class OutputBroadcasterTests
{
    private readonly Mock<IHubContext<ShellVMHub>> _hubContextMock;
    private readonly Mock<IHubClients> _hubClientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly OutputBroadcaster _broadcaster;

    public OutputBroadcasterTests()
    {
        _clientProxyMock  = new Mock<IClientProxy>();
        _hubClientsMock   = new Mock<IHubClients>();
        _groupManagerMock = new Mock<IGroupManager>();
        _hubContextMock   = new Mock<IHubContext<ShellVMHub>>();

        _hubClientsMock.Setup(c => c.Group(It.IsAny<string>()))
                       .Returns(_clientProxyMock.Object);

        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
        _hubContextMock.Setup(h => h.Groups).Returns(_groupManagerMock.Object);

        _clientProxyMock
            .Setup(p => p.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _groupManagerMock
            .Setup(g => g.AddToGroupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _groupManagerMock
            .Setup(g => g.RemoveFromGroupAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _broadcaster = new OutputBroadcaster(
            _hubContextMock.Object,
            null,
            Microsoft.Extensions.Options.Options.Create(new ShellVMConfig()),
            NullLogger<OutputBroadcaster>.Instance);
    }

    [Fact]
    public async Task BroadcastChunkAsync_SendsToSessionGroup()
    {
        var sessionId = Guid.NewGuid();
        var chunk     = new OutputChunk(sessionId, OutputStream.Stdout, "hello", 1, DateTimeOffset.UtcNow);

        await _broadcaster.BroadcastChunkAsync(chunk, CancellationToken.None);

        _hubClientsMock.Verify(c => c.Group($"session:{sessionId}"), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "ReceiveOutput", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubscribeAsync_AddsConnectionToSessionGroup()
    {
        var connectionId = "conn-123";
        var sessionId    = Guid.NewGuid();

        await _broadcaster.SubscribeAsync(connectionId, sessionId, CancellationToken.None);

        _groupManagerMock.Verify(g => g.AddToGroupAsync(
            connectionId, $"session:{sessionId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesConnectionFromGroup()
    {
        var connectionId = "conn-456";
        var sessionId    = Guid.NewGuid();

        await _broadcaster.SubscribeAsync(connectionId, sessionId, CancellationToken.None);
        await _broadcaster.UnsubscribeAsync(connectionId, CancellationToken.None);

        _groupManagerMock.Verify(g => g.RemoveFromGroupAsync(
            connectionId, $"session:{sessionId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_IsIdempotent_WhenNotSubscribed()
    {
        // Should not throw or call RemoveFromGroupAsync
        await _broadcaster.UnsubscribeAsync("unknown-conn", CancellationToken.None);

        _groupManagerMock.Verify(g => g.RemoveFromGroupAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
