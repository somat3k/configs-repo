namespace MLS.Core.Constants;

public static partial class MessageTypes
{
    /// <summary>Unique or sequential ID successfully generated.</summary>
    public const string IdGenerated = "ID_GENERATED";

    /// <summary>Task ID successfully generated.</summary>
    public const string TaskIdGenerated = "TASK_ID_GENERATED";

    /// <summary>Client subscribed to a topic.</summary>
    public const string TopicSubscribed = "TOPIC_SUBSCRIBED";

    /// <summary>Client unsubscribed from a topic.</summary>
    public const string TopicUnsubscribed = "TOPIC_UNSUBSCRIBED";

    /// <summary>Message published to a topic.</summary>
    public const string TopicMessage = "TOPIC_MESSAGE";

    /// <summary>Module container successfully started.</summary>
    public const string ModuleStarted = "MODULE_STARTED";

    /// <summary>Module container successfully stopped.</summary>
    public const string ModuleStopped = "MODULE_STOPPED";

    /// <summary>Module status polled and returned.</summary>
    public const string ModuleStatusUpdate = "MODULE_STATUS_UPDATE";

    /// <summary>Sandbox script execution completed.</summary>
    public const string SandboxExecuted = "SANDBOX_EXECUTED";

    /// <summary>Sandbox execution result returned to the caller.</summary>
    public const string SandboxResult = "SANDBOX_RESULT";

    /// <summary>Container image registered in the registry.</summary>
    public const string ContainerRegistered = "CONTAINER_REGISTERED";

    /// <summary>Health check result recorded for a container image.</summary>
    public const string HealthCheckUpdated = "HEALTH_CHECK_UPDATED";

    /// <summary>Module endpoint registered with Network Mask.</summary>
    public const string EndpointRegistered = "ENDPOINT_REGISTERED";

    /// <summary>Module endpoint resolved by Network Mask.</summary>
    public const string EndpointResolved = "ENDPOINT_RESOLVED";
}
