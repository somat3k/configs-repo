namespace MLS.Core.Designer;

/// <summary>
/// Extends <see cref="IBlockElement"/> with autonomous data-source behaviour (Block-as-ONE pattern).
/// An action tile maintains internal state, runs a background fetch/update loop, and streams
/// data to all connected output sockets independently of whether it has received an input signal.
/// </summary>
/// <remarks>
/// <para>
/// Each action tile type is a <b>singleton</b> per strategy graph: the <c>BlockRegistry</c>
/// enforces one live instance per block type per graph.  Multiple canvas tiles may subscribe
/// to that ONE instance's output socket.
/// </para>
/// <para>
/// On connection, the consumer immediately receives the current snapshot via
/// <see cref="GetCurrentSnapshot"/>, then continues to receive streamed updates.
/// If no consumers are connected, emitted signals are silently discarded (pass-through default).
/// </para>
/// </remarks>
public interface IActionTile : IBlockElement
{
    /// <summary>
    /// <see langword="true"/> when the tile is running its internal fetch/update loop.
    /// Set by <see cref="StartAsync"/> and cleared by <see cref="StopAsync"/>.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Start the autonomous fetch/update loop.
    /// Called by the Designer engine when the strategy graph is activated.
    /// </summary>
    /// <param name="ct">Cancellation token — cancelled when the graph is stopped.</param>
    Task StartAsync(CancellationToken ct);

    /// <summary>Stop the fetch/update loop gracefully.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task StopAsync(CancellationToken ct);

    /// <summary>
    /// Retrieve the current snapshot of this tile's internal state.
    /// Used to hydrate newly connected consumers without waiting for the next update cycle.
    /// Returns <see langword="null"/> if the tile has not yet emitted its first update.
    /// </summary>
    BlockSignal? GetCurrentSnapshot();
}
