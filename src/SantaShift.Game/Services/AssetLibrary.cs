using Raylib_cs;
using SantaShift.Core.Content;

namespace SantaShift.Game.Services;

/// <summary>
/// Hands out Blender models by file name. Swappable. If a model is missing or fails to load,
/// <see cref="TryGetModel"/> returns false and the view draws grey-box shapes instead,
/// so art can be added or replaced at any time without code changes.
/// </summary>
public interface IAssetLibrary : IDisposable
{
    bool TryGetModel(string? fileName, out Model model);
}

public sealed class AssetLibrary : IAssetLibrary
{
    public const string ElfModel = "elf.glb";
    public const string KitModel = "kit_crate.glb";
    public const string RibbonModel = "ribbon_bow.glb";
    public const string RoomModel = "room.glb";

    private readonly string _modelDirectory;
    private readonly Dictionary<string, Model> _models = new();

    public AssetLibrary(string modelDirectory) => _modelDirectory = modelDirectory;

    /// <summary>Loads every model the content mentions, once, at startup (no loading during play).</summary>
    public void Preload(GameContent content, Action<Model>? prepare = null)
    {
        var names = new HashSet<string> { ElfModel, KitModel, RibbonModel, RoomModel };
        foreach (var toy in content.Toys) if (toy.Model != null) names.Add(toy.Model);
        foreach (var station in content.Stations) if (station.Model != null) names.Add(station.Model);

        foreach (var name in names)
        {
            string path = Path.Combine(_modelDirectory, name);
            if (!File.Exists(path)) continue;
            var model = Raylib.LoadModel(path);
            if (model.MeshCount > 0) { prepare?.Invoke(model); _models[name] = model; }
            else Raylib.UnloadModel(model);
        }
    }

    public IReadOnlyCollection<string> LoadedModels => _models.Keys;

    public bool TryGetModel(string? fileName, out Model model)
    {
        if (fileName != null && _models.TryGetValue(fileName, out model)) return true;
        model = default;
        return false;
    }

    public void Dispose()
    {
        foreach (var model in _models.Values) Raylib.UnloadModel(model);
        _models.Clear();
    }
}
