using FluentAssertions;
using MLS.Transactions.Services;
using Xunit;

namespace MLS.Transactions.Tests.Services;

/// <summary>Unit tests for <see cref="ModuleIdentity"/>.</summary>
public sealed class ModuleIdentityTests
{
    [Fact]
    public void Id_IsEmptyGuid_ByDefault()
    {
        var identity = new ModuleIdentity();
        identity.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public void Id_CanBeSet_AndRead()
    {
        var identity = new ModuleIdentity();
        var expected = Guid.NewGuid();

        identity.Id = expected;

        identity.Id.Should().Be(expected);
    }

    [Fact]
    public void Id_IsNonEmpty_AfterRegistration()
    {
        var identity = new ModuleIdentity { Id = Guid.NewGuid() };
        identity.Id.Should().NotBe(Guid.Empty);
    }
}
