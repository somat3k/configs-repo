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
/// </remarks>
public static class EnvelopeValidator
{
    private static readonly char[] PayloadSchemaSeparator = [':'];

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

        var errors = new List<ValidationError>();

        ValidateType(envelope.Type, errors);
        ValidateVersion(envelope.Version, errors);
        ValidateModuleId(envelope.ModuleId, errors);
        ValidateTimestamp(envelope.Timestamp, errors);
        ValidatePayload(envelope.Payload, errors);

        return errors.Count == 0 ? ValidationResult.Valid : ValidationResult.Fail(errors);
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

        var errors = new List<ValidationError>();

        ValidateType(envelope.Type, errors);
        ValidateVersion(envelope.Version, errors);
        ValidateModuleId(envelope.ModuleId, errors);
        ValidateTimestamp(envelope.Timestamp, errors);
        ValidatePayload(envelope.Payload, errors);

        // Session 04 extended field checks
        ValidateTraceId(envelope.TraceId, errors);
        ValidateCorrelationId(envelope.CorrelationId, errors);
        ValidatePayloadSchema(envelope.PayloadSchema, errors);
        ValidatePriority(envelope.Priority, errors);
        ValidateRoutingScope(envelope.RoutingScope, envelope.TargetModule, envelope.Topic, errors);

        return errors.Count == 0 ? ValidationResult.Valid : ValidationResult.Fail(errors);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void ValidateType(string? type, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(type))
            errors.Add(new ValidationError("type", "Field 'type' must be a non-null, non-empty string."));
    }

    private static void ValidateVersion(int version, List<ValidationError> errors)
    {
        if (version < 1)
            errors.Add(new ValidationError("version", $"Field 'version' must be ≥ 1, but was {version}."));
    }

    private static void ValidateModuleId(string? moduleId, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            errors.Add(new ValidationError("module_id", "Field 'module_id' must be a non-null, non-empty string."));
    }

    private static void ValidateTimestamp(DateTimeOffset timestamp, List<ValidationError> errors)
    {
        if (timestamp == default)
            errors.Add(new ValidationError("timestamp", "Field 'timestamp' must be set to a non-default UTC value."));

        // Clock-skew tolerance: reject timestamps more than 5 seconds in the future.
        if (timestamp > DateTimeOffset.UtcNow.AddSeconds(5))
            errors.Add(new ValidationError("timestamp",
                $"Field 'timestamp' is more than 5 seconds in the future (value: {timestamp:O})."));
    }

    private static void ValidatePayload(System.Text.Json.JsonElement payload, List<ValidationError> errors)
    {
        if (payload.ValueKind != System.Text.Json.JsonValueKind.Object)
            errors.Add(new ValidationError("payload",
                $"Field 'payload' must be a JSON object, but was {payload.ValueKind}."));
    }

    private static void ValidateTraceId(string? traceId, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(traceId))
            errors.Add(new ValidationError("trace_id",
                "Field 'trace_id' must be a non-null, non-empty W3C traceparent-compatible string."));
    }

    private static void ValidateCorrelationId(Guid correlationId, List<ValidationError> errors)
    {
        if (correlationId == Guid.Empty)
            errors.Add(new ValidationError("correlation_id",
                "Field 'correlation_id' must be a non-empty Guid."));
    }

    private static void ValidatePayloadSchema(string? payloadSchema, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(payloadSchema))
        {
            errors.Add(new ValidationError("payload_schema",
                "Field 'payload_schema' must be a non-null string in format '<schema>:<version>'."));
            return;
        }

        var parts = payloadSchema.Split(PayloadSchemaSeparator, 2, StringSplitOptions.None);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            errors.Add(new ValidationError("payload_schema",
                $"Field 'payload_schema' must follow the format '<schema>:<version>' (received: '{payloadSchema}')."));
    }

    private static void ValidatePriority(int priority, List<ValidationError> errors)
    {
        if (priority is < 0 or > 9)
            errors.Add(new ValidationError("priority",
                $"Field 'priority' must be between 0 and 9 inclusive, but was {priority}."));
    }

    private static void ValidateRoutingScope(
        RoutingScope routingScope,
        string? targetModule,
        string? topic,
        List<ValidationError> errors)
    {
        if (routingScope == RoutingScope.Module && string.IsNullOrWhiteSpace(targetModule))
            errors.Add(new ValidationError("target_module",
                "Field 'target_module' must be set when 'routing_scope' is Module."));

        if (routingScope == RoutingScope.Topic && string.IsNullOrWhiteSpace(topic))
            errors.Add(new ValidationError("topic",
                "Field 'topic' must be set when 'routing_scope' is Topic."));
    }
}
