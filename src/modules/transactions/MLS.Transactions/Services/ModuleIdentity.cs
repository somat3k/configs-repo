namespace MLS.Transactions.Services;

/// <summary>
/// Thread-safe singleton that holds the module's registered GUID from Block Controller.
/// </summary>
public sealed class ModuleIdentity
{
    private Guid _id = Guid.Empty;
    private readonly object _lock = new();

    /// <summary>The module ID assigned by Block Controller on registration.</summary>
    public Guid Id
    {
        get { lock (_lock) return _id; }
        set { lock (_lock) _id = value; }
    }
}
