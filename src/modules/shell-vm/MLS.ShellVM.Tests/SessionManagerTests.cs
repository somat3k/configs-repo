using System.Threading.Channels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MLS.ShellVM.Interfaces;
using MLS.ShellVM.Models;
using MLS.ShellVM.Persistence;
using MLS.ShellVM.Services;
using Xunit;

namespace MLS.ShellVM.Tests;

/// <summary>
/// Tests for <see cref="SessionManager"/> using an in-memory EF Core database
/// and mocked dependencies.
/// </summary>
public sealed class SessionManagerTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly DbContextOptions<ShellVMDbContext> _dbOpts;
    private readonly Mock<IDbContextFactory<ShellVMDbContext>> _dbFactoryMock;
    private readonly Mock<IPtyProvider> _ptyMock;
    private readonly SessionManager _manager;

    public SessionManagerTests()
    {
        _dbOpts = new DbContextOptionsBuilder<ShellVMDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _dbFactoryMock = new Mock<IDbContextFactory<ShellVMDbContext>>();
        _dbFactoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ShellVMDbContext(_dbOpts));

        _ptyMock = new Mock<IPtyProvider>();

        var config = Options.Create(new ShellVMConfig
        {
            AllowedShells         = ["/bin/sh", "/bin/bash", "python3"],
            DefaultShell          = "/bin/sh",
            MaxConcurrentSessions = 4,
        });

        _manager = new SessionManager(
            _dbFactoryMock.Object,
            null,
            _ptyMock.Object,
            config,
            NullLogger<SessionManager>.Instance);
    }

    public void Dispose() { }

    // ── CreateSessionAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSessionAsync_ReturnsSession_WithCreatedState()
    {
        var req     = new CreateSessionRequest("test-session", "/bin/sh");
        var session = await _manager.CreateSessionAsync(req, CancellationToken.None);

        session.Should().NotBeNull();
        session.Block.Label.Should().Be("test-session");
        session.Block.Shell.Should().Be("/bin/sh");
        session.Block.State.Should().Be(ExecutionBlockState.Created);
    }

    [Fact]
    public async Task CreateSessionAsync_PersistsBlockToDatabase()
    {
        var req = new CreateSessionRequest("persist-test", "/bin/bash");
        var session = await _manager.CreateSessionAsync(req, CancellationToken.None);

        await using var db = new ShellVMDbContext(_dbOpts);
        var entity = await db.ExecutionBlocks.FindAsync(session.Block.Id);
        entity.Should().NotBeNull();
        entity!.Label.Should().Be("persist-test");
        entity.Shell.Should().Be("/bin/bash");
        entity.State.Should().Be(ExecutionBlockState.Created.ToString());
    }

    [Fact]
    public async Task CreateSessionAsync_Throws_WhenShellNotAllowed()
    {
        var req = new CreateSessionRequest("bad-shell", "zsh");

        var act = async () => await _manager.CreateSessionAsync(req, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*allow-list*");
    }

    [Fact]
    public async Task CreateSessionAsync_Throws_WhenSessionLimitReached()
    {
        var config = Options.Create(new ShellVMConfig
        {
            AllowedShells         = ["/bin/sh"],
            DefaultShell          = "/bin/sh",
            MaxConcurrentSessions = 0,  // no capacity
        });

        var manager = new SessionManager(
            _dbFactoryMock.Object, null, _ptyMock.Object, config,
            NullLogger<SessionManager>.Instance);

        var act = async () => await manager.CreateSessionAsync(
            new CreateSessionRequest("x", "/bin/sh"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*Session limit*");
    }

    [Fact]
    public async Task CreateSessionAsync_UsesDefaultShell_WhenShellEmpty()
    {
        var req     = new CreateSessionRequest("default-shell", Shell: "");
        var session = await _manager.CreateSessionAsync(req, CancellationToken.None);

        session.Block.Shell.Should().Be("/bin/sh");
    }

    // ── GetSessionAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSessionAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _manager.GetSessionAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSessionAsync_ReturnsSession_AfterCreate()
    {
        var session = await _manager.CreateSessionAsync(
            new CreateSessionRequest("find-me", "/bin/sh"), CancellationToken.None);

        var found = await _manager.GetSessionAsync(session.Block.Id, CancellationToken.None);
        found.Should().NotBeNull();
        found!.Block.Id.Should().Be(session.Block.Id);
    }

    // ── TerminateSessionAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task TerminateSessionAsync_RemovesSession_FromMemory()
    {
        _ptyMock.Setup(p => p.KillAsync(It.IsAny<PtyHandle>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

        var session = await _manager.CreateSessionAsync(
            new CreateSessionRequest("to-terminate", "/bin/sh"), CancellationToken.None);

        await _manager.TerminateSessionAsync(session.Block.Id, graceful: true, CancellationToken.None);

        var found = await _manager.GetSessionAsync(session.Block.Id, CancellationToken.None);
        found.Should().BeNull();
    }

    [Fact]
    public async Task TerminateSessionAsync_IsIdempotent_WhenSessionNotFound()
    {
        // Should not throw
        await _manager.TerminateSessionAsync(Guid.NewGuid(), graceful: true, CancellationToken.None);
    }

    // ── GetSessionsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSessionsAsync_ReturnsAllSessions_WhenNoFilter()
    {
        await _manager.CreateSessionAsync(new CreateSessionRequest("s1", "/bin/sh"), CancellationToken.None);
        await _manager.CreateSessionAsync(new CreateSessionRequest("s2", "/bin/bash"), CancellationToken.None);

        var all = await _manager.GetSessionsAsync(null, CancellationToken.None).ToListAsync();
        all.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetSessionsAsync_FiltersBy_State()
    {
        await _manager.CreateSessionAsync(new CreateSessionRequest("created", "/bin/sh"), CancellationToken.None);

        var created = await _manager.GetSessionsAsync(ExecutionBlockState.Created, CancellationToken.None).ToListAsync();
        created.Should().NotBeEmpty();
        created.Should().OnlyContain(s => s.Block.State == ExecutionBlockState.Created);
    }

    // ── ActiveSessionCount ────────────────────────────────────────────────────

    [Fact]
    public async Task ActiveSessionCount_ExcludesTerminatedSessions()
    {
        _ptyMock.Setup(p => p.KillAsync(It.IsAny<PtyHandle>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

        // Created state is not counted as "active" (not Running/Starting/Paused)
        await _manager.CreateSessionAsync(new CreateSessionRequest("inactive", "/bin/sh"), CancellationToken.None);
        _manager.ActiveSessionCount.Should().Be(0);
    }
}

/// <summary>Extension to collect <see cref="IAsyncEnumerable{T}"/> into a list in tests.</summary>
file static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }
}
