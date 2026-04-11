namespace MLS.Core.Constants;

public static partial class MessageTypes
{
    // ── Block Controller — Runtime Governor (Session 02) ─────────────────────────

    /// <summary>A module's capability declaration was updated or first registered.</summary>
    public const string ModuleCapabilityUpdated = "MODULE_CAPABILITY_UPDATED";

    /// <summary>A module has transitioned to the Degraded health state.</summary>
    public const string ModuleDegraded = "MODULE_DEGRADED";

    /// <summary>A module has been marked Draining and will accept no new work.</summary>
    public const string ModuleDrained = "MODULE_DRAINED";

    /// <summary>A module has transitioned to the Offline state (missed heartbeat threshold).</summary>
    public const string ModuleOffline = "MODULE_OFFLINE";

    /// <summary>A module has recovered and returned to the Healthy state.</summary>
    public const string ModuleRecovered = "MODULE_RECOVERED";

    /// <summary>A module has been placed in Maintenance mode by an operator.</summary>
    public const string ModuleMaintenance = "MODULE_MAINTENANCE";

    /// <summary>A module has been quarantined by an operator.</summary>
    public const string ModuleQuarantined = "MODULE_QUARANTINED";

    /// <summary>An execution request was rejected by the route admission service.</summary>
    public const string RouteRejected = "ROUTE_REJECTED";
}
