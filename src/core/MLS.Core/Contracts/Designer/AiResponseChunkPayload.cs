namespace MLS.Core.Contracts.Designer;

/// <summary>
/// Payload for <c>AI_RESPONSE_CHUNK</c> — one streaming text fragment from ai-hub to web-app.
/// </summary>
/// <param name="ChunkIndex">Zero-based sequence index.</param>
/// <param name="Text">Text fragment to append to the chat panel.</param>
/// <param name="IsFinal">When <see langword="true"/>, this is the last chunk; close the stream.</param>
/// <param name="FunctionCallsPending">Number of SK tool calls still in-flight.</param>
public sealed record AiResponseChunkPayload(
    int ChunkIndex,
    string Text,
    bool IsFinal,
    int FunctionCallsPending);
