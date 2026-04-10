namespace MLS.MLRuntime.Services;

/// <summary>
/// Singleton that carries the module GUID used for hub connections and outbound envelopes.
/// Shared by <see cref="BlockControllerClient"/> (registration) and <see cref="InferenceWorker"/>
/// (hub connection + envelope authoring) so that the block-controller always sees a consistent
/// identity across all protocol layers.
/// </summary>
public sealed class ModuleIdentity
{
    private readonly object _lock = new();
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the module GUID. Pre-populated with a random value at startup;
    /// replaced by <see cref="BlockControllerClient"/> with the server-assigned GUID
    /// upon successful registration.
    /// </summary>
    public Guid Id
    {
        get { lock (_lock) return _id; }
        internal set { lock (_lock) _id = value; }
    }
}
