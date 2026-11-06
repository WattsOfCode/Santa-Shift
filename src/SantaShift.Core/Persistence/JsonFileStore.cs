using System.Text.Json;
using SantaShift.Core.Content;

namespace SantaShift.Core.Persistence;

/// <summary>Stores each record as name.json in one folder. Missing, unreadable or corrupt files load as null.</summary>
public sealed class JsonFileStore(string directory) : ISaveStore
{
    public string Directory { get; } = directory;

    /// <summary>The default save folder: %AppData%/SantaShift on Windows.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SantaShift");

    public T? Load<T>(string name) where T : class
    {
        try
        {
            string path = PathFor(name);
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.Options) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    public bool Save<T>(string name, T value) where T : class
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string path = PathFor(name);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonDefaults.Options));
            File.Move(temp, path, overwrite: true); // write-then-rename: a crash never leaves half a file
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    private string PathFor(string name) => Path.Combine(Directory, name + ".json");
}
