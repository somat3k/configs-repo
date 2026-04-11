namespace MLS.Core.Transport;

/// <summary>
/// Transport class for a BCG inter-module message, as defined in Session 04
/// of the BCG transport constitution.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><see cref="ClassA"/> — Authoritative machine contracts (gRPC/protobuf).</item>
///   <item><see cref="ClassB"/> — Live event and stream fabric (WebSocket/SignalR).</item>
///   <item><see cref="ClassC"/> — External trigger and compatibility ingress (HTTP/JSON).</item>
///   <item><see cref="ClassD"/> — Artifact and large-object reference transport.</item>
/// </list>
/// </remarks>
public enum TransportClass
{
    /// <summary>
    /// Authoritative machine contracts — gRPC / protobuf lanes for registration,
    /// execution, tensor transformation, and stateful runtime commands.
    /// </summary>
    ClassA = 1,

    /// <summary>
    /// Live event and stream fabric — WebSocket / SignalR for operator feeds,
    /// partial outputs, subscription updates, and live observability.
    /// </summary>
    ClassB = 2,

    /// <summary>
    /// External trigger and compatibility ingress — HTTP/JSON for webhooks,
    /// external partner integrations, and management endpoints.
    /// </summary>
    ClassC = 3,

    /// <summary>
    /// Artifact and large-object reference transport — metadata envelope inline,
    /// artifact body via Redis / PostgreSQL / IPFS reference.
    /// </summary>
    ClassD = 4,
}
