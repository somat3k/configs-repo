using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLS.ShellVM.Models;
using MLS.ShellVM.Persistence;
using MLS.ShellVM.Services;
using Xunit;

namespace MLS.ShellVM.Tests;

/// <summary>
/// Tests for <see cref="AuditLogger"/> using an in-memory EF Core database.
/// </summary>
public sealed class AuditLoggerTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly DbContextOptions<ShellVMDbContext> _dbOpts;
    private readonly AuditLogger _logger;

    public AuditLoggerTests()
    {
        _dbOpts = new DbContextOptionsBuilder<ShellVMDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        var factoryMock = new Moq.Mock<Microsoft.EntityFrameworkCore.IDbContextFactory<ShellVMDbContext>>();
        factoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ShellVMDbContext(_dbOpts));

        _logger = new AuditLogger(factoryMock.Object, NullLogger<AuditLogger>.Instance);
    }

    [Fact]
    public async Task LogCommandStartAsync_PersistsEntry()
    {
        var entry = new AuditEntry
        {
            Id        = Guid.NewGuid(),
            BlockId   = Guid.NewGuid(),
            Command   = "echo hello",
            StartedAt = DateTimeOffset.UtcNow,
            ModuleId  = "trader",
        };

        await _logger.LogCommandStartAsync(entry, CancellationToken.None);

        await using var db = new ShellVMDbContext(_dbOpts);
        var entity = await db.AuditLog.FindAsync(entry.Id);
        entity.Should().NotBeNull();
        entity!.Command.Should().Be("echo hello");
        entity.ModuleId.Should().Be("trader");
    }

    [Fact]
    public async Task LogCommandEndAsync_UpdatesEntry()
    {
        var commandId = Guid.NewGuid();
        var entry = new AuditEntry
        {
            Id        = commandId,
            BlockId   = Guid.NewGuid(),
            Command   = "ls -la",
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
        };

        await _logger.LogCommandStartAsync(entry, CancellationToken.None);
        await _logger.LogCommandEndAsync(commandId, 0, TimeSpan.FromSeconds(5), CancellationToken.None);

        await using var db = new ShellVMDbContext(_dbOpts);
        var entity = await db.AuditLog.FindAsync(commandId);
        entity!.ExitCode.Should().Be(0);
        entity.DurationMs.Should().NotBeNull();
        entity.DurationMs!.Value.Should().BeInRange(4000, 10000);
        entity.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LogCommandEndAsync_IsNoOp_WhenEntryNotFound()
    {
        // Should not throw
        await _logger.LogCommandEndAsync(Guid.NewGuid(), 0, TimeSpan.Zero, CancellationToken.None);
    }

    [Fact]
    public async Task QueryAsync_FiltersBySessionId()
    {
        var blockId = Guid.NewGuid();
        var other   = Guid.NewGuid();

        await _logger.LogCommandStartAsync(
            new AuditEntry { Id = Guid.NewGuid(), BlockId = blockId, Command = "a", StartedAt = DateTimeOffset.UtcNow },
            CancellationToken.None);
        await _logger.LogCommandStartAsync(
            new AuditEntry { Id = Guid.NewGuid(), BlockId = other, Command = "b", StartedAt = DateTimeOffset.UtcNow },
            CancellationToken.None);

        var query   = new AuditQuery(SessionId: blockId);
        var results = new List<AuditEntry>();
        await foreach (var e in _logger.QueryAsync(query, CancellationToken.None))
            results.Add(e);

        results.Should().HaveCount(1);
        results[0].BlockId.Should().Be(blockId);
        results[0].Command.Should().Be("a");
    }

    [Fact]
    public async Task QueryAsync_RespectsLimit()
    {
        var blockId = Guid.NewGuid();
        for (var i = 0; i < 10; i++)
        {
            await _logger.LogCommandStartAsync(
                new AuditEntry { Id = Guid.NewGuid(), BlockId = blockId, Command = $"cmd-{i}", StartedAt = DateTimeOffset.UtcNow },
                CancellationToken.None);
        }

        var query   = new AuditQuery(SessionId: blockId, Limit: 3);
        var results = new List<AuditEntry>();
        await foreach (var e in _logger.QueryAsync(query, CancellationToken.None))
            results.Add(e);

        results.Should().HaveCount(3);
    }
}
