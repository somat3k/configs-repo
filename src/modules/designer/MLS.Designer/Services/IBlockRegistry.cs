using MLS.Core.Designer;

namespace MLS.Designer.Services;

/// <summary>
/// Central catalog of all available block types in the Designer module.
/// Blocks register themselves on startup; the registry provides metadata for the canvas UI.
/// </summary>
public interface IBlockRegistry
{
    /// <summary>Register a block type with the given key.</summary>
    /// <typeparam name="T">Concrete block type implementing <see cref="IBlockElement"/>.</typeparam>
    /// <param name="key">Registry key, must match <see cref="IBlockElement.BlockType"/>.</param>
    void Register<T>(string key) where T : IBlockElement, new();

    /// <summary>Get all registered block metadata records.</summary>
    IReadOnlyList<BlockMetadata> GetAll();

    /// <summary>Get metadata for a single block type by key.</summary>
    /// <returns><c>null</c> if the key is not registered.</returns>
    BlockMetadata? GetByKey(string key);

    /// <summary>Create a fresh instance of a registered block type.</summary>
    /// <returns><c>null</c> if the key is not registered.</returns>
    IBlockElement? CreateInstance(string key);

    /// <summary>
    /// Register a block type using an explicit factory delegate.
    /// Use this for blocks that require constructor arguments (e.g. HttpClient).
    /// The factory is called once to read metadata, then stored for subsequent <see cref="CreateInstance"/> calls.
    /// </summary>
    /// <param name="key">Registry key, must match <see cref="IBlockElement.BlockType"/>.</param>
    /// <param name="factory">Factory that creates a fresh block instance.</param>
    void Register(string key, Func<IBlockElement> factory);
}
