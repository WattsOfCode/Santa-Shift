using System.Text.Json;
using SantaShift.Core.Content;

namespace SantaShift.Core.Persistence;

/// <summary>Keeps records in memory only. Used when the save folder cannot be written, and in tests.</summary>
public sealed class MemoryStore : ISaveStore
{
    private readonly Dictionary<string, string> _data = new();

    public T? Load<T>(string name) where T : class =>
        _data.TryGetValue(name, out var json) ? JsonSerializer.Deserialize<T>(json, JsonDefaults.Options) : null;

    public bool Save<T>(string name, T value) where T : class
    {
        _data[name] = JsonSerializer.Serialize(value, JsonDefaults.Options); // copy, so later edits don't leak in
        return true;
    }
}
