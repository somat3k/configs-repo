using System.Text.Json;
using MLS.Core.Constants;

namespace MLS.Core.Contracts;

/// <summary>
/// Universal inter-module message envelope for all WebSocket communication
/// in the MLS platform. All fields are required; <see cref="Payload"/> must
/// be a non-null JSON object.
/// </summary>
/// <remarks>
/// Rules:
/// <list type="number">
///   <item><see cref="Version"/> MUST be ≥ 1.</item>
///   <item><see cref="Type"/> MUST reference a <see cref="MessageTypes"/> constant.</item>
///   <item><see cref="SessionId"/> MUST be a new <see cref="Guid"/> per message.</item>
///   <item><see cref="ModuleId"/> MUST match the module's registered identity.</item>
///   <item><see cref="Payload"/> MUST be a non-null JSON object.</item>
/// </list>
/// </remarks>
/// <param name="Type">Message type constant from <see cref="MessageTypes"/>.</param>
/// <param name="Version">Schema version, must be ≥ 1.</param>
/// <param name="SessionId">UUID v4 correlation identifier — use <see cref="Guid.NewGuid"/>.</param>
/// <param name="ModuleId">Sender's registered module ID.</param>
/// <param name="Timestamp">UTC creation timestamp.</param>
/// <param name="Payload">Strongly-typed payload serialised as a <see cref="JsonElement"/>.</param>
public sealed record EnvelopePayload(
    string Type,
    int Version,
    Guid SessionId,
    string ModuleId,
    DateTimeOffset Timestamp,
    JsonElement Payload)
{
    /// <summary>
    /// Convenience factory — creates an envelope with a new session ID and the current UTC time.
    /// </summary>
    public static EnvelopePayload Create<TPayload>(
        string type,
        string moduleId,
        TPayload payload,
        int version = 1)
        where TPayload : notnull =>
        new(
            Type:      type,
            Version:   version,
            SessionId: Guid.NewGuid(),
            ModuleId:  moduleId,
            Timestamp: DateTimeOffset.UtcNow,
            Payload:   JsonSerializer.SerializeToElement(payload));
}
