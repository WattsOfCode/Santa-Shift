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

    /// <summary>The animations stored in a model file (for the elf: Idle, Walk, Work). Empty if it has none.</summary>
    IReadOnlyList<ModelAnimation> Animations(string fileName);

    /// <summary>
    /// A painted picture (a toy's, by toy id) and the lit square it is drawn on: 1 x 1 m, lying flat facing up, already
    /// wearing the picture. False when there is no such picture.
    /// </summary>
    bool TryGetPicture(string? id, out Texture2D picture, out Model quad);
}

public sealed class AssetLibrary : IAssetLibrary
{
    public const string ElfModel = "elf.glb";
    public const string KitModel = "kit_crate.glb";
    public const string RibbonModel = "ribbon_bow.glb";
    public const string RoomModel = "room.glb";
    public const string HammerModel = "hammer.glb";
    public const string CoalModel = "coal_lump.glb";
    public const string ShovelModel = "shovel.glb";
    public const string SupplyCrateModel = "supply_crate.glb";
    public const string SpoolModel = "ribbon_spool.glb";
    public const string GuideArrowModel = "guide_arrow.glb";

    private readonly string _modelDirectory;
    private readonly Dictionary<string, Model> _models = new();
    private readonly Dictionary<string, (nint Pointer, ModelAnimation[] List)> _animations = new();
    private readonly Dictionary<string, Texture2D> _pictures = new();
    private Model _quad;
    private bool _hasQuad;
    private Action<Model>? _prepare;

    public AssetLibrary(string modelDirectory) => _modelDirectory = modelDirectory;

    /// <summary>Keeps a painted picture (the library unloads it). Pictures are drawn on a shared, lit square.</summary>
    public void AddPicture(string id, Texture2D picture)
    {
        if (!_hasQuad)
        {
            _quad = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(1, 1, 1, 1));
            _prepare?.Invoke(_quad);
            _hasQuad = true;
        }
        if (_pictures.Remove(id, out var old)) Raylib.UnloadTexture(old);
        _pictures[id] = picture;
    }

    public unsafe bool TryGetPicture(string? id, out Texture2D picture, out Model quad)
    {
        quad = _quad;
        if (id != null && _hasQuad && _pictures.TryGetValue(id, out picture))
        {
            _quad.Materials[0].Maps[(int)MaterialMapIndex.Albedo].Texture = picture;
            return true;
        }
        picture = default;
        return false;
    }

    /// <summary>Loads every model the content mentions, once, at startup (no loading during play).</summary>
    public unsafe void Preload(GameContent content, Action<Model>? prepare = null)
    {
        _prepare = prepare;
        var names = new HashSet<string> { ElfModel, KitModel, RibbonModel, RoomModel, HammerModel, CoalModel, ShovelModel, SupplyCrateModel, SpoolModel, GuideArrowModel };
        foreach (var toy in content.Toys)
        {
            if (toy.Model != null) names.Add(toy.Model);
            if (toy.BotchedModel != null) names.Add(toy.BotchedModel);
            foreach (var part in toy.Parts) if (part.Model != null) names.Add(part.Model);
        }
        foreach (var station in content.Stations) if (station.Model != null) names.Add(station.Model);
        foreach (var prop in content.Props) names.Add(PropFile(prop.Model));

        foreach (var name in names)
        {
            string path = Path.Combine(_modelDirectory, name);
            if (!File.Exists(path)) continue;
            var model = Raylib.LoadModel(path);
            if (model.MeshCount > 0) { prepare?.Invoke(model); _models[name] = model; }
            else { Raylib.UnloadModel(model); continue; }

            if (model.Skeleton.BoneCount == 0) continue;
            int count = 0;
            var anims = Raylib.LoadModelAnimations(path, ref count);
            if (anims == null || count == 0) continue;
            var list = new ModelAnimation[count];
            for (int i = 0; i < count; i++)
            {
                list[i] = anims[i];
#if DEBUG
                Console.WriteLine($"[anim] {name}: {list[i].NameToString()}, {list[i].KeyFrameCount} key frames, {list[i].BoneCount} bones");
#endif
            }
            _animations[name] = ((nint)anims, list);
        }
    }

    public IReadOnlyList<ModelAnimation> Animations(string fileName) =>
        _animations.TryGetValue(fileName, out var a) ? a.List : [];

    public IReadOnlyCollection<string> LoadedModels => _models.Keys;

    /// <summary>The model file for a prop name from the room layout ("prop_tree" -> "prop_tree.glb").</summary>
    public static string PropFile(string model) => model.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ? model : model + ".glb";

    public bool TryGetModel(string? fileName, out Model model)
    {
        if (fileName != null && _models.TryGetValue(fileName, out model)) return true;
        model = default;
        return false;
    }

    public unsafe void Dispose()
    {
        foreach (var (pointer, list) in _animations.Values) Raylib.UnloadModelAnimations((ModelAnimation*)pointer, list.Length);
        _animations.Clear();
        foreach (var model in _models.Values) Raylib.UnloadModel(model);
        _models.Clear();
        foreach (var picture in _pictures.Values) Raylib.UnloadTexture(picture);
        _pictures.Clear();
        if (_hasQuad)
        {
            // The pictures are unloaded already: leave Raylib's default texture for the model to (not) unload.
            _quad.Materials[0].Maps[(int)MaterialMapIndex.Albedo].Texture = new Texture2D { Id = Rlgl.GetTextureIdDefault() };
            Raylib.UnloadModel(_quad);
            _hasQuad = false;
        }
    }
}
