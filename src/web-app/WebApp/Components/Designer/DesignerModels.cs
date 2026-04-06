using MLS.Core.Designer;

namespace MLS.WebApp.Components.Designer;

/// <summary>View model for a block instance on the designer canvas.</summary>
public sealed class BlockViewModel(Guid blockId, string blockType, double x, double y,
    List<SocketViewModel> inputSockets, List<SocketViewModel> outputSockets)
{
    public Guid BlockId { get; } = blockId;
    public string BlockType { get; } = blockType;
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public List<SocketViewModel> InputSockets { get; } = inputSockets;
    public List<SocketViewModel> OutputSockets { get; } = outputSockets;
}

/// <summary>View model for a socket on a block node.</summary>
public sealed record SocketViewModel(Guid SocketId, string Name, BlockSocketType SocketType);

/// <summary>View model for a connection (wire) between two sockets.</summary>
public sealed record ConnectionViewModel(
    Guid ConnectionId,
    Guid FromBlockId,
    Guid FromSocketId,
    Guid ToBlockId,
    Guid ToSocketId);
