using MLS.Core.Contracts;

namespace MLS.Core.Transport;

/// <summary>
/// Validates BCG envelope mandatory fields before admission into the internal fabric.
/// </summary>
/// <remarks>
/// Implements the universal intake validation rules defined in
/// <c>docs/bcg/validation-admission-rules.md</c> (Session 04).
/// <para>
/// All validation failures are encoded in the returned <see cref="ValidationResult"/>.
/// This validator never throws on malformed input — callers inspect the result.
/// </para>
/// <para>
/// The <c>type</c> field is validated as non-null and non-empty. Full verification that
/// <c>type</c> references a known <see cref="Constants.MessageTypes"/> constant is a
/// routing-layer concern enforced by the Block Controller at admission time, not by this
/// structural validator.
/// </para>
/// <para>
/// The <c>trace_id</c> field is validated against the W3C traceparent wire format
/// (<c>version-traceid-parentid-flags</c>, 55 lowercase hex characters with separators).
/// </para>
/// <para>
/// This validator is allocation-free on the happy (valid) path.
/// </para>
/// </remarks>
public static class EnvelopeValidator
{
    // W3C traceparent: "00-<32hex>-<16hex>-<2hex>" = 55 chars total.
    // Separator positions: [2], [35], [52].
    private const int TraceParentLength = 55;
    private const int TraceParentSep1 = 2;
    private const int TraceParentSep2 = 35;
    private const int TraceParentSep3 = 52;

    /// <summary>
    /// Validates the mandatory fields of a base <see cref="EnvelopePayload"/>.
    /// </summary>
    /// <param name="envelope">The envelope to validate. Must not be null.</param>
    /// <returns>
    /// <see cref="ValidationResult.Valid"/> when all checks pass;
    /// a failed result with one <see cref="ValidationError"/> per violation otherwise.
    /// </returns>
    public static ValidationResult Validate(EnvelopePayload envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        List<ValidationError>? errors = null;

        ValidateType(envelope.Type, ref errors);
        ValidateVersion(envelope.Version, ref errors);
        ValidateModuleId(envelope.ModuleId, ref errors);
        ValidateTimestamp(envelope.Timestamp, ref errors);
        ValidatePayload(envelope.Payload, ref errors);

        return errors is null ? ValidationResult.Valid : ValidationResult.Fail(errors);
    }

    /// <summary>
    /// Validates the mandatory fields of an <see cref="EnvelopeV2"/>, including
    /// all Session 04 extended fields.
    /// </summary>
    /// <param name="envelope">The extended envelope to validate. Must not be null.</param>
    /// <returns>
    /// <see cref="ValidationResult.Valid"/> when all checks pass;
    /// a failed result with one <see cref="ValidationError"/> per violation otherwise.
    /// </returns>
    public static ValidationResult Validate(EnvelopeV2 envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        List<ValidationError>? errors = null;

        ValidateType(envelope.Type, ref errors);
        ValidateVersion(envelope.Version, ref errors);
        ValidateModuleId(envelope.ModuleId, ref errors);
        ValidateTimestamp(envelope.Timestamp, ref errors);
        ValidatePayload(envelope.Payload, ref errors);

        // Session 04 extended field checks
        ValidateTraceId(envelope.TraceId, ref errors);
        ValidateCorrelationId(envelope.CorrelationId, ref errors);
        ValidatePayloadSchema(envelope.PayloadSchema, ref errors);
        ValidatePriority(envelope.Priority, ref errors);
        ValidateTransportClassEnum(envelope.TransportClass, ref errors);
        ValidateRoutingScopeEnum(envelope.RoutingScope, ref errors);
        ValidateRoutingScopeConstraints(envelope.RoutingScope, envelope.TargetModule, envelope.Topic, ref errors);

        return errors is null ? ValidationResult.Valid : ValidationResult.Fail(errors);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void ValidateType(string? type, ref List<ValidationError>? errors)
    {
        if (string.IsNullOrWhiteSpace(type))
            AddError(ref errors, "type", "Field 'type' must be a non-null, non-empty string.");
    }

    private static void ValidateVersion(int version, ref List<ValidationError>? errors)
    {
        if (version < 1)
            AddError(ref errors, "version", $"Field 'version' must be ≥ 1, but was {version}.");
    }

    private static void ValidateModuleId(string? moduleId, ref List<ValidationError>? errors)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            AddError(ref errors, "module_id", "Field 'module_id' must be a non-null, non-empty string.");
    }

    private static void ValidateTimestamp(DateTimeOffset timestamp, ref List<ValidationError>? errors)
    {
        if (timestamp == default)
        {
            AddError(ref errors, "timestamp", "Field 'timestamp' must be set to a non-default UTC value.");
            return;
        }

        // Clock-skew tolerance: reject timestamps more than 5 seconds in the future.
        if (timestamp > DateTimeOffset.UtcNow.AddSeconds(5))
            AddError(ref errors, "timestamp",
                $"Field 'timestamp' is more than 5 seconds in the future (value: {timestamp:O}).");
    }

    private static void ValidatePayload(System.Text.Json.JsonElement payload, ref List<ValidationError>? errors)
    {
        if (payload.ValueKind != System.Text.Json.JsonValueKind.Object)
            AddError(ref errors, "payload",
                $"Field 'payload' must be a JSON object, but was {payload.ValueKind}.");
    }

    private static void ValidateTraceId(string? traceId, ref List<ValidationError>? errors)
    {
        if (string.IsNullOrWhiteSpace(traceId))
        {
            AddError(ref errors, "trace_id",
                "Field 'trace_id' must be a non-null W3C traceparent-compatible string.");
            return;
        }

        if (!IsValidTraceParent(traceId))
            AddError(ref errors, "trace_id",
                $"Field 'trace_id' must be a valid W3C traceparent (format: 'xx-<32hex>-<16hex>-xx'), but was '{traceId}'.");
    }

    private static void ValidateCorrelationId(Guid correlationId, ref List<ValidationError>? errors)
    {
        if (correlationId == Guid.Empty)
            AddError(ref errors, "correlation_id", "Field 'correlation_id' must be a non-empty Guid.");
    }

    private static void ValidatePayloadSchema(string? payloadSchema, ref List<ValidationError>? errors)
    {
        if (string.IsNullOrWhiteSpace(payloadSchema))
        {
            AddError(ref errors, "payload_schema",
                "Field 'payload_schema' must be a non-null string in format '<schema>:<version>'.");
            return;
        }

        // Use IndexOf to avoid string.Split allocation on the fast path.
        var colonIdx = payloadSchema.IndexOf(':');
        if (colonIdx <= 0 || colonIdx == payloadSchema.Length - 1)
            AddError(ref errors, "payload_schema",
                $"Field 'payload_schema' must follow the format '<schema>:<version>' (received: '{payloadSchema}').");
    }

    private static void ValidatePriority(int priority, ref List<ValidationError>? errors)
    {
        if (priority is < 0 or > 9)
            AddError(ref errors, "priority",
                $"Field 'priority' must be between 0 and 9 inclusive, but was {priority}.");
    }

    private static void ValidateTransportClassEnum(TransportClass transportClass, ref List<ValidationError>? errors)
    {
        if (transportClass is < TransportClass.ClassA or > TransportClass.ClassD)
            AddError(ref errors, "transport_class",
                $"Field 'transport_class' must be one of ClassA–ClassD, but was {(int)transportClass}.");
    }

    private static void ValidateRoutingScopeEnum(RoutingScope routingScope, ref List<ValidationError>? errors)
    {
        if (routingScope is < RoutingScope.Broadcast or > RoutingScope.Session)
            AddError(ref errors, "routing_scope",
                $"Field 'routing_scope' must be one of Broadcast/Module/Topic/Session, but was {(int)routingScope}.");
    }

    private static void ValidateRoutingScopeConstraints(
        RoutingScope routingScope,
        string? targetModule,
        string? topic,
        ref List<ValidationError>? errors)
    {
        if (routingScope == RoutingScope.Module && string.IsNullOrWhiteSpace(targetModule))
            AddError(ref errors, "target_module",
                "Field 'target_module' must be set when 'routing_scope' is Module.");

        if (routingScope == RoutingScope.Topic && string.IsNullOrWhiteSpace(topic))
            AddError(ref errors, "topic",
                "Field 'topic' must be set when 'routing_scope' is Topic.");
    }

    // ── Allocation helper ─────────────────────────────────────────────────────

    private static void AddError(ref List<ValidationError>? errors, string field, string reason) =>
        (errors ??= []).Add(new ValidationError(field, reason));

    // ── W3C traceparent format check ──────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="traceId"/> matches the
    /// W3C traceparent wire format: <c>xx-{32hex}-{16hex}-xx</c> (55 characters,
    /// lowercase hex only).
    /// </summary>
    private static bool IsValidTraceParent(string traceId)
    {
        if (traceId.Length != TraceParentLength) return false;
        if (traceId[TraceParentSep1] != '-') return false;
        if (traceId[TraceParentSep2] != '-') return false;
        if (traceId[TraceParentSep3] != '-') return false;

        return IsLowerHex(traceId.AsSpan(0, TraceParentSep1))                            // version  (2)
            && IsLowerHex(traceId.AsSpan(TraceParentSep1 + 1, 32))                       // trace-id (32)
            && IsLowerHex(traceId.AsSpan(TraceParentSep2 + 1, 16))                       // parent   (16)
            && IsLowerHex(traceId.AsSpan(TraceParentSep3 + 1, TraceParentLength - TraceParentSep3 - 1)); // flags (2)
    }

    private static bool IsLowerHex(ReadOnlySpan<char> s)
    {
        foreach (var c in s)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                return false;
        }
        return true;
    }
}
