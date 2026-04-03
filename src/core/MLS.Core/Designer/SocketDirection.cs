namespace MLS.Core.Designer;

/// <summary>Direction of a block socket — input receives data, output emits data.</summary>
public enum SocketDirection
{
    /// <summary>Socket receives data from a connected output socket.</summary>
    Input,
    /// <summary>Socket emits processed data to connected input sockets.</summary>
    Output,
}
