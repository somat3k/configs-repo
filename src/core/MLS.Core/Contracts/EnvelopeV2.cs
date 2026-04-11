using System.Text.Json.Serialization;
using MLS.Core.Transport;

namespace MLS.Core.Contracts;

/// <summary>
/// Extended inter-module message envelope (Session 04).
/// <para>
/// Adds cross-cutting governance fields — trace, correlation, causation, schema, transport
/// class, routing scope, and priority — while preserving full backward compatibility with
/// <see cref="EnvelopePayload"/>. All base fields are retained unchanged.
/// </para>
/// </summary>
/// <remarks>
/// Rules (Session 04 additions):
/// <list type="number">
///   <item><see cref="TraceId"/> MUST be W3C <c>traceparent</c>-compatible and stable across a request lineage.</item>
///   <item><see cref="CorrelationId"/> MUST be present on all Class A messages.</item>
///   <item><see cref="CausationId"/> MUST reference the immediate causing message; null for root messages.</item>
///   <item><see cref="PayloadSchema"/> format: <c>"&lt;proto_package.MessageName&gt;:&lt;version&gt;"</c>.</item>
///   <item><see cref="TransportClass"/> MUST be declared; defaults to <see cref="Transport.TransportClass.ClassB"/>.</item>
///   <item><see cref="RoutingScope"/> MUST be declared; defaults to <see cref="Transport.RoutingScope.Broadcast"/>.</item>
///   <item><see cref="Priority"/> must be 0–9 (0 = lowest, 9 = highest).</item>
/// </list>
/// </remarks>
/// <param name="Type">Message type constant from <see cref="Constants.MessageTypes"/>.</param>
/// <param name="Version">Schema version ≥ 1.</param>
/// <param name="SessionId">UUID v4 correlation identifier.</param>
/// <param name="ModuleId">Sender's registered module ID.</param>
/// <param name="Timestamp">UTC creation timestamp.</param>
/// <param name="Payload">Strongly-typed payload serialised as a <see cref="System.Text.Json.JsonElement"/>.</param>
/// <param name="TraceId">W3C traceparent-compatible distributed trace identifier.</param>
/// <param name="CorrelationId">Ties related messages across a logical operation.</param>
/// <param name="CausationId">The message that directly caused this one. Null for root messages.</param>
/// <param name="PayloadSchema">Schema name and version, e.g. <c>"bcg.module.RegisterModule:1"</c>.</param>
/// <param name="TransportClass">Declared transport class (A/B/C/D).</param>
/// <param name="RoutingScope">Routing scope (Broadcast/Module/Topic/Session).</param>
/// <param name="Priority">Message priority 0–9.</param>
/// <param name="TargetModule">Target module ID when <see cref="RoutingScope"/> is <see cref="Transport.RoutingScope.Module"/>.</param>
/// <param name="Topic">Topic name when <see cref="RoutingScope"/> is <see cref="Transport.RoutingScope.Topic"/>.</param>
/// <param name="TaskId">Task identifier when part of a governed task.</param>
/// <param name="BlockId">Block identifier when scoped to a specific block.</param>
public sealed record EnvelopeV2(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("session_id")] Guid SessionId,
    [property: JsonPropertyName("module_id")] string ModuleId,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("payload")] System.Text.Json.JsonElement Payload,
    [property: JsonPropertyName("trace_id")] string TraceId,
    [property: JsonPropertyName("correlation_id")] Guid CorrelationId,
    [property: JsonPropertyName("causation_id")] Guid? CausationId,
    [property: JsonPropertyName("payload_schema")] string PayloadSchema,
    [property: JsonPropertyName("transport_class")] TransportClass TransportClass,
    [property: JsonPropertyName("routing_scope")] RoutingScope RoutingScope,
    [property: JsonPropertyName("priority")] int Priority,
    [property: JsonPropertyName("target_module")] string? TargetModule = null,
    [property: JsonPropertyName("topic")] string? Topic = null,
    [property: JsonPropertyName("task_id")] string? TaskId = null,
    [property: JsonPropertyName("block_id")] string? BlockId = null)
{
    /// <summary>
    /// Creates an <see cref="EnvelopeV2"/> with a new session ID, a new correlation ID,
    /// and the current UTC timestamp.
    /// </summary>
    /// <typeparam name="TPayload">Payload type — must serialize to a JSON object.</typeparam>
    /// <param name="type">Message type constant.</param>
    /// <param name="moduleId">Sender module ID.</param>
    /// <param name="payload">Payload instance.</param>
    /// <param name="traceId">W3C traceparent-compatible trace ID. Pass the current activity trace ID.</param>
    /// <param name="payloadSchema">Schema name and version string, e.g. <c>"bcg.module.RegisterModule:1"</c>.</param>
    /// <param name="transportClass">Transport class for this message.</param>
    /// <param name="routingScope">Routing scope for this message.</param>
    /// <param name="version">Envelope version, must be ≥ 1.</param>
    /// <param name="priority">Priority 0–9.</param>
    /// <param name="causationId">ID of the causing message, or null for root messages.</param>
    /// <param name="targetModule">Target module, required when <paramref name="routingScope"/> is <see cref="Transport.RoutingScope.Module"/>.</param>
    /// <param name="topic">Topic name, required when <paramref name="routingScope"/> is <see cref="Transport.RoutingScope.Topic"/>.</param>
    /// <param name="taskId">Task ID, optional.</param>
    /// <param name="blockId">Block ID, optional.</param>
    /// <exception cref="ArgumentException">When required string fields are null or whitespace, or payload is not a JSON object.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="version"/> &lt; 1 or <paramref name="priority"/> is outside 0–9.</exception>
    public static EnvelopeV2 Create<TPayload>(
        string type,
        string moduleId,
        TPayload payload,
        string traceId,
        string payloadSchema,
        TransportClass transportClass = TransportClass.ClassB,
        RoutingScope routingScope = RoutingScope.Broadcast,
        int version = 1,
        int priority = 0,
        Guid? causationId = null,
        string? targetModule = null,
        string? topic = null,
        string? taskId = null,
        string? blockId = null)
        where TPayload : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSchema);

        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), version,
                "Envelope version must be greater than or equal to 1.");

        if (priority is < 0 or > 9)
            throw new ArgumentOutOfRangeException(nameof(priority), priority,
                "Envelope priority must be between 0 and 9 inclusive.");

        var serialisedPayload = System.Text.Json.JsonSerializer.SerializeToElement(payload);
        if (serialisedPayload.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new ArgumentException(
                "Envelope payload must serialise to a non-null JSON object.", nameof(payload));

        return new EnvelopeV2(
            Type: type,
            Version: version,
            SessionId: Guid.NewGuid(),
            ModuleId: moduleId,
            Timestamp: DateTimeOffset.UtcNow,
            Payload: serialisedPayload,
            TraceId: traceId,
            CorrelationId: Guid.NewGuid(),
            CausationId: causationId,
            PayloadSchema: payloadSchema,
            TransportClass: transportClass,
            RoutingScope: routingScope,
            Priority: priority,
            TargetModule: targetModule,
            Topic: topic,
            TaskId: taskId,
            BlockId: blockId);
    }

    /// <summary>
    /// Converts this <see cref="EnvelopeV2"/> to a base <see cref="EnvelopePayload"/>
    /// for interoperability with modules that consume only the base envelope.
    /// </summary>
    public EnvelopePayload ToBaseEnvelope() =>
        new(Type, Version, SessionId, ModuleId, Timestamp, Payload);
}
