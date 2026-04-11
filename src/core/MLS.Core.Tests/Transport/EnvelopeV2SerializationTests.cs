using System.Text.Json;
using FluentAssertions;
using MLS.Core.Contracts;
using MLS.Core.Transport;
using Xunit;

namespace MLS.Core.Tests.Transport;

public class EnvelopeV2SerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void RoundTrip_Json_PreservesAllMandatoryFields()
    {
        var original = MakeEnvelopeV2(causationId: Guid.NewGuid());

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<EnvelopeV2>(json, JsonOptions);

        roundTripped.Should().NotBeNull();
        roundTripped!.Type.Should().Be(original.Type);
        roundTripped.Version.Should().Be(original.Version);
        roundTripped.ModuleId.Should().Be(original.ModuleId);
        roundTripped.TraceId.Should().Be(original.TraceId);
        roundTripped.CorrelationId.Should().Be(original.CorrelationId);
        roundTripped.CausationId.Should().Be(original.CausationId);
        roundTripped.PayloadSchema.Should().Be(original.PayloadSchema);
        roundTripped.TransportClass.Should().Be(original.TransportClass);
        roundTripped.RoutingScope.Should().Be(original.RoutingScope);
        roundTripped.Priority.Should().Be(original.Priority);
    }

    [Fact]
    public void RoundTrip_Json_CorrelationIdSurvives()
    {
        var original = MakeEnvelopeV2();

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<EnvelopeV2>(json, JsonOptions);

        roundTripped!.CorrelationId.Should().NotBe(Guid.Empty);
        roundTripped.CorrelationId.Should().Be(original.CorrelationId);
    }

    [Fact]
    public void RoundTrip_Json_CausationIdNullWhenNotSet()
    {
        var original = MakeEnvelopeV2(causationId: null);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<EnvelopeV2>(json, JsonOptions);

        roundTripped!.CausationId.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_Json_OptionalFieldsNullWhenNotSet()
    {
        var original = MakeEnvelopeV2();

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<EnvelopeV2>(json, JsonOptions);

        roundTripped!.TargetModule.Should().BeNull();
        roundTripped.Topic.Should().BeNull();
        roundTripped.TaskId.Should().BeNull();
        roundTripped.BlockId.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_Json_OptionalFieldsPreservedWhenSet()
    {
        var original = EnvelopeV2.Create(
            type: "TENSOR_ROUTED",
            moduleId: "ml-runtime",
            payload: new { ok = true },
            traceId: "00-abc123-def456-01",
            payloadSchema: "bcg.tensor.BcgTensorProto:1",
            transportClass: TransportClass.ClassA,
            routingScope: RoutingScope.Module,
            targetModule: "trader",
            taskId: "task-001",
            blockId: "block-007");

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<EnvelopeV2>(json, JsonOptions);

        roundTripped!.TargetModule.Should().Be("trader");
        roundTripped.TaskId.Should().Be("task-001");
        roundTripped.BlockId.Should().Be("block-007");
    }

    [Fact]
    public void Json_ContainsExpectedPropertyNames()
    {
        var envelope = MakeEnvelopeV2();
        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        json.Should().Contain("\"type\"");
        json.Should().Contain("\"version\"");
        json.Should().Contain("\"session_id\"");
        json.Should().Contain("\"module_id\"");
        json.Should().Contain("\"timestamp\"");
        json.Should().Contain("\"trace_id\"");
        json.Should().Contain("\"correlation_id\"");
        json.Should().Contain("\"payload_schema\"");
        json.Should().Contain("\"transport_class\"");
        json.Should().Contain("\"routing_scope\"");
        json.Should().Contain("\"priority\"");
    }

    [Fact]
    public void ToBaseEnvelope_PreservesBaseFields()
    {
        var v2 = MakeEnvelopeV2();
        var baseEnvelope = v2.ToBaseEnvelope();

        baseEnvelope.Type.Should().Be(v2.Type);
        baseEnvelope.Version.Should().Be(v2.Version);
        baseEnvelope.SessionId.Should().Be(v2.SessionId);
        baseEnvelope.ModuleId.Should().Be(v2.ModuleId);
        baseEnvelope.Timestamp.Should().Be(v2.Timestamp);
    }

    [Fact]
    public void Create_AssignsNewCorrelationIdEachTime()
    {
        var e1 = MakeEnvelopeV2();
        var e2 = MakeEnvelopeV2();

        e1.CorrelationId.Should().NotBe(e2.CorrelationId);
    }

    [Fact]
    public void Create_AssignsNewSessionIdEachTime()
    {
        var e1 = MakeEnvelopeV2();
        var e2 = MakeEnvelopeV2();

        e1.SessionId.Should().NotBe(e2.SessionId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EnvelopeV2 MakeEnvelopeV2(Guid? causationId = null) =>
        EnvelopeV2.Create(
            type: "MODULE_REGISTER",
            moduleId: "trader",
            payload: new { ok = true },
            traceId: "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            payloadSchema: "bcg.module.RegisterModule:1",
            transportClass: TransportClass.ClassA,
            routingScope: RoutingScope.Broadcast,
            priority: 3,
            causationId: causationId);
}
