namespace MLS.Core.Transport;

/// <summary>
/// Routing scope for a BCG envelope, controlling how the Block Controller
/// delivers the message to its target(s).
/// </summary>
public enum RoutingScope
{
    /// <summary>
    /// Message is delivered to all current subscribers on the broadcast group.
    /// Best-effort; no delivery guarantee for late joiners.
    /// </summary>
    Broadcast = 1,

    /// <summary>
    /// Message is routed to a specific registered module by <c>target_module</c>.
    /// </summary>
    Module = 2,

    /// <summary>
    /// Message is delivered to all subscribers of a named topic.
    /// </summary>
    Topic = 3,

    /// <summary>
    /// Message is scoped to a specific operator or shell session.
    /// </summary>
    Session = 4,
}
