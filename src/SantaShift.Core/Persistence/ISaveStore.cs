namespace SantaShift.Core.Persistence;

/// <summary>
/// Saves small records (settings, best scores). Swappable: the game uses <see cref="JsonFileStore"/>
/// and falls back to <see cref="MemoryStore"/> if the disk cannot be written.
/// Implementations never throw; failures return null or false.
/// </summary>
public interface ISaveStore
{
    T? Load<T>(string name) where T : class;
    bool Save<T>(string name, T value) where T : class;
}
