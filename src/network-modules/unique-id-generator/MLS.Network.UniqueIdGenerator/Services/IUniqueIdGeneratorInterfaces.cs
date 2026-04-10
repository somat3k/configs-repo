namespace MLS.Network.UniqueIdGenerator.Services;

/// <summary>Service interface for unique ID generation operations.</summary>
public interface IUniqueIdService
{
    /// <summary>Generates a new UUID as a 32-character hex string.</summary>
    string GenerateUuid();

    /// <summary>Generates a monotonically-increasing sequential ID for the given prefix.</summary>
    /// <param name="prefix">Prefix namespace for the counter.</param>
    long GenerateSequentialId(string prefix);

    /// <summary>Streams <paramref name="count"/> UUIDs asynchronously.</summary>
    /// <param name="count">Number of UUIDs to yield.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<string> StreamUuidsAsync(int count, CancellationToken ct);
}
