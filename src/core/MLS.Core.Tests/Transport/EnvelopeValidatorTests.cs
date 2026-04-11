using System.Text.Json;
using FluentAssertions;
using MLS.Core.Contracts;
using MLS.Core.Transport;
using Xunit;

namespace MLS.Core.Tests.Transport;

public class EnvelopeValidatorTests
{
    // ── Base EnvelopePayload validation ──────────────────────────────────────

    [Fact]
    public void Validate_BaseEnvelope_Valid_ReturnsValid()
    {
        var envelope = MakeBaseEnvelope();
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_BaseEnvelope_NullType_ReturnsError()
    {
        var envelope = MakeBaseEnvelope() with { Type = null! };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "type");
    }

    [Fact]
    public void Validate_BaseEnvelope_EmptyType_ReturnsError()
    {
        var envelope = MakeBaseEnvelope() with { Type = "   " };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "type");
    }

    [Fact]
    public void Validate_BaseEnvelope_VersionZero_ReturnsError()
    {
        var envelope = MakeBaseEnvelope() with { Version = 0 };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "version");
    }

    [Fact]
    public void Validate_BaseEnvelope_NegativeVersion_ReturnsError()
    {
        var envelope = MakeBaseEnvelope() with { Version = -1 };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "version");
    }

    [Fact]
    public void Validate_BaseEnvelope_NullModuleId_ReturnsError()
    {
        var envelope = MakeBaseEnvelope() with { ModuleId = null! };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "module_id");
    }

    [Fact]
    public void Validate_BaseEnvelope_EmptyModuleId_ReturnsError()
    {
        var envelope = MakeBaseEnvelope() with { ModuleId = "" };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "module_id");
    }

    [Fact]
    public void Validate_BaseEnvelope_NonObjectPayload_ReturnsError()
    {
        var arrayPayload = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 });
        var envelope = MakeBaseEnvelope() with { Payload = arrayPayload };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "payload");
    }

    [Fact]
    public void Validate_BaseEnvelope_FutureTimestamp_ReturnsError()
    {
        var envelope = MakeBaseEnvelope() with { Timestamp = DateTimeOffset.UtcNow.AddSeconds(10) };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "timestamp");
    }

    [Fact]
    public void Validate_BaseEnvelope_DefaultTimestamp_ReturnsError()
    {
        var envelope = MakeBaseEnvelope() with { Timestamp = default };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "timestamp");
    }

    [Fact]
    public void Validate_BaseEnvelope_MultipleErrors_ReturnsAllErrors()
    {
        var envelope = MakeBaseEnvelope() with { Type = null!, Version = 0, ModuleId = "" };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
    }

    // ── EnvelopeV2 validation ─────────────────────────────────────────────────

    [Fact]
    public void Validate_EnvelopeV2_Valid_ReturnsValid()
    {
        var envelope = MakeEnvelopeV2();
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EnvelopeV2_NullType_ReturnsError()
    {
        var envelope = MakeEnvelopeV2() with { Type = null! };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "type");
    }

    [Fact]
    public void Validate_EnvelopeV2_NullTraceId_ReturnsError()
    {
        var envelope = MakeEnvelopeV2() with { TraceId = null! };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "trace_id");
    }

    [Fact]
    public void Validate_EnvelopeV2_EmptyCorrelationId_ReturnsError()
    {
        var envelope = MakeEnvelopeV2() with { CorrelationId = Guid.Empty };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "correlation_id");
    }

    [Fact]
    public void Validate_EnvelopeV2_NullPayloadSchema_ReturnsError()
    {
        var envelope = MakeEnvelopeV2() with { PayloadSchema = null! };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "payload_schema");
    }

    [Fact]
    public void Validate_EnvelopeV2_MalformedPayloadSchema_ReturnsError()
    {
        var envelope = MakeEnvelopeV2() with { PayloadSchema = "no-colon-here" };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "payload_schema");
    }

    [Fact]
    public void Validate_EnvelopeV2_PriorityTooHigh_ReturnsError()
    {
        var envelope = MakeEnvelopeV2() with { Priority = 10 };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "priority");
    }

    [Fact]
    public void Validate_EnvelopeV2_NegativePriority_ReturnsError()
    {
        var envelope = MakeEnvelopeV2() with { Priority = -1 };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "priority");
    }

    [Fact]
    public void Validate_EnvelopeV2_ModuleScope_MissingTargetModule_ReturnsError()
    {
        var envelope = MakeEnvelopeV2() with
        {
            RoutingScope = RoutingScope.Module,
            TargetModule = null
        };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "target_module");
    }

    [Fact]
    public void Validate_EnvelopeV2_TopicScope_MissingTopic_ReturnsError()
    {
        var envelope = MakeEnvelopeV2() with
        {
            RoutingScope = RoutingScope.Topic,
            Topic = null
        };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Field == "topic");
    }

    [Fact]
    public void Validate_EnvelopeV2_ModuleScope_WithTargetModule_ReturnsValid()
    {
        var envelope = MakeEnvelopeV2() with
        {
            RoutingScope = RoutingScope.Module,
            TargetModule = "trader"
        };
        var result = EnvelopeValidator.Validate(envelope);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ThrowsArgumentNull_WhenBaseEnvelopeIsNull()
    {
        var act = () => EnvelopeValidator.Validate((EnvelopePayload)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_ThrowsArgumentNull_WhenEnvelopeV2IsNull()
    {
        var act = () => EnvelopeValidator.Validate((EnvelopeV2)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EnvelopePayload MakeBaseEnvelope() =>
        EnvelopePayload.Create(
            type: "MODULE_REGISTER",
            moduleId: "trader",
            payload: new { ok = true });

    private static EnvelopeV2 MakeEnvelopeV2() =>
        EnvelopeV2.Create(
            type: "MODULE_REGISTER",
            moduleId: "trader",
            payload: new { ok = true },
            traceId: "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            payloadSchema: "bcg.module.RegisterModule:1",
            transportClass: TransportClass.ClassA,
            routingScope: RoutingScope.Broadcast,
            priority: 5);
}
