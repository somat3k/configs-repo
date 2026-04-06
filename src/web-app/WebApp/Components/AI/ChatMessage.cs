namespace MLS.WebApp.Components.AI;

/// <summary>
/// Immutable chat message model for the AI chat panel UI.
/// </summary>
/// <param name="Id">Unique identifier for this message.</param>
/// <param name="Role">Either <c>"user"</c> or <c>"assistant"</c>.</param>
/// <param name="Content">Full accumulated text content.</param>
/// <param name="IsStreaming"><see langword="true"/> while the assistant is still generating tokens.</param>
/// <param name="Timestamp">UTC time the message was created.</param>
public sealed record ChatMessage(
    Guid Id,
    string Role,
    string Content,
    bool IsStreaming,
    DateTimeOffset Timestamp);
