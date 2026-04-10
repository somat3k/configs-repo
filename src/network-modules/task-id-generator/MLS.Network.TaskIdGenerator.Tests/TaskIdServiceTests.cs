using Xunit;
using FluentAssertions;
using MLS.Network.TaskIdGenerator.Services;

namespace MLS.Network.TaskIdGenerator.Tests;

public sealed class TaskIdServiceTests
{
    private readonly TaskIdService _sut = new();

    [Fact]
    public void GenerateTaskId_StartsWithTask()
    {
        var id = _sut.GenerateTaskId("trader", "order");
        id.Should().StartWith("task:trader:order:");
    }

    [Fact]
    public void ValidateTaskId_ReturnsTrueForValidId()
    {
        var id = _sut.GenerateTaskId("ml-runtime", "inference");
        _sut.ValidateTaskId(id).Should().BeTrue();
    }

    [Fact]
    public void ValidateTaskId_ReturnsFalseForInvalidId()
    {
        _sut.ValidateTaskId("invalid-id").Should().BeFalse();
        _sut.ValidateTaskId(string.Empty).Should().BeFalse();
        _sut.ValidateTaskId("task:only:three").Should().BeFalse();
    }

    [Fact]
    public void ParseTaskId_ReturnsCorrectComponents()
    {
        var id = _sut.GenerateTaskId("broker", "trade");
        var components = _sut.ParseTaskId(id);

        components.Should().NotBeNull();
        components!.ModuleId.Should().Be("broker");
        components.TaskType.Should().Be("trade");
        components.SequenceNumber.Should().Be(1L);
    }

    [Fact]
    public void GenerateTaskId_IncrementsPerKey()
    {
        _sut.GenerateTaskId("module-a", "type-x");
        var second = _sut.GenerateTaskId("module-a", "type-x");
        var components = _sut.ParseTaskId(second);
        components!.SequenceNumber.Should().Be(2L);
    }
}
