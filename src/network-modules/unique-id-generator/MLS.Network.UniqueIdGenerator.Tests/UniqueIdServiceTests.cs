using Xunit;
using FluentAssertions;
using MLS.Network.UniqueIdGenerator.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MLS.Network.UniqueIdGenerator.Tests;

public sealed class UniqueIdServiceTests
{
    private readonly UniqueIdService _sut = new();

    [Fact]
    public void GenerateUuid_Returns32CharHexString()
    {
        var id = _sut.GenerateUuid();

        id.Should().HaveLength(32);
        id.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void GenerateSequentialId_IncrementsPerCall()
    {
        var first  = _sut.GenerateSequentialId("test");
        var second = _sut.GenerateSequentialId("test");

        second.Should().Be(first + 1);
    }

    [Fact]
    public async Task StreamUuidsAsync_YieldsExactCount()
    {
        var results = new List<string>();
        await foreach (var id in _sut.StreamUuidsAsync(5, CancellationToken.None))
            results.Add(id);

        results.Should().HaveCount(5);
    }

    [Fact]
    public void GenerateSequentialId_IsolatedPerPrefix()
    {
        _sut.GenerateSequentialId("prefix-a");
        _sut.GenerateSequentialId("prefix-a");
        var bFirst = _sut.GenerateSequentialId("prefix-b");

        bFirst.Should().Be(1L);
    }

    [Fact]
    public void GenerateSequentialId_ConcurrentSafety()
    {
        var results = new System.Collections.Concurrent.ConcurrentBag<long>();
        Parallel.For(0, 100, _ => results.Add(_sut.GenerateSequentialId("concurrent")));

        results.Should().HaveCount(100);
        results.Distinct().Should().HaveCount(100);
    }
}
