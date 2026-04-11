using FluentAssertions;
using MLS.Core.Transport;
using Xunit;

namespace MLS.Core.Tests.Transport;

public class TransportClassEnumTests
{
    [Fact]
    public void TransportClass_AllValues_AreDefined()
    {
        var values = Enum.GetValues<TransportClass>();
        values.Should().Contain(TransportClass.ClassA);
        values.Should().Contain(TransportClass.ClassB);
        values.Should().Contain(TransportClass.ClassC);
        values.Should().Contain(TransportClass.ClassD);
    }

    [Fact]
    public void TransportClass_ClassA_HasExpectedIntegerValue()
    {
        ((int)TransportClass.ClassA).Should().Be(1);
    }

    [Fact]
    public void TransportClass_ClassB_HasExpectedIntegerValue()
    {
        ((int)TransportClass.ClassB).Should().Be(2);
    }

    [Fact]
    public void TransportClass_ClassC_HasExpectedIntegerValue()
    {
        ((int)TransportClass.ClassC).Should().Be(3);
    }

    [Fact]
    public void TransportClass_ClassD_HasExpectedIntegerValue()
    {
        ((int)TransportClass.ClassD).Should().Be(4);
    }

    [Theory]
    [InlineData(TransportClass.ClassA)]
    [InlineData(TransportClass.ClassB)]
    [InlineData(TransportClass.ClassC)]
    [InlineData(TransportClass.ClassD)]
    public void TransportClass_AllValues_ParseFromString(TransportClass value)
    {
        var name = value.ToString();
        var parsed = Enum.Parse<TransportClass>(name);
        parsed.Should().Be(value);
    }

    [Fact]
    public void RoutingScope_AllValues_AreDefined()
    {
        var values = Enum.GetValues<RoutingScope>();
        values.Should().Contain(RoutingScope.Broadcast);
        values.Should().Contain(RoutingScope.Module);
        values.Should().Contain(RoutingScope.Topic);
        values.Should().Contain(RoutingScope.Session);
    }

    [Fact]
    public void RoutingScope_Broadcast_HasExpectedIntegerValue()
    {
        ((int)RoutingScope.Broadcast).Should().Be(1);
    }

    [Fact]
    public void RoutingScope_Module_HasExpectedIntegerValue()
    {
        ((int)RoutingScope.Module).Should().Be(2);
    }

    [Fact]
    public void RoutingScope_Topic_HasExpectedIntegerValue()
    {
        ((int)RoutingScope.Topic).Should().Be(3);
    }

    [Fact]
    public void RoutingScope_Session_HasExpectedIntegerValue()
    {
        ((int)RoutingScope.Session).Should().Be(4);
    }

    [Theory]
    [InlineData(RoutingScope.Broadcast)]
    [InlineData(RoutingScope.Module)]
    [InlineData(RoutingScope.Topic)]
    [InlineData(RoutingScope.Session)]
    public void RoutingScope_AllValues_ParseFromString(RoutingScope value)
    {
        var name = value.ToString();
        var parsed = Enum.Parse<RoutingScope>(name);
        parsed.Should().Be(value);
    }

    [Theory]
    [InlineData(TransportClass.ClassA)]
    [InlineData(TransportClass.ClassB)]
    [InlineData(TransportClass.ClassC)]
    [InlineData(TransportClass.ClassD)]
    public void TransportClass_AllValues_SerializeToJson(TransportClass value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        json.Should().NotBeNullOrEmpty();
        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<TransportClass>(json);
        roundTripped.Should().Be(value);
    }

    [Theory]
    [InlineData(RoutingScope.Broadcast)]
    [InlineData(RoutingScope.Module)]
    [InlineData(RoutingScope.Topic)]
    [InlineData(RoutingScope.Session)]
    public void RoutingScope_AllValues_SerializeToJson(RoutingScope value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        json.Should().NotBeNullOrEmpty();
        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<RoutingScope>(json);
        roundTripped.Should().Be(value);
    }
}
