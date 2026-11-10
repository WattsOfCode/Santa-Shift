using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.World;

/// <summary>
/// Draws kits and toys. Uses the Blender model when it exists, otherwise simple grey-box shapes.
/// Every item state looks different: crate = kit, toy shape = assembled, gold bow = ribbon.
/// </summary>
public sealed class ItemView(GameContent content, IAssetLibrary assets)
{
    /// <summary>Draws an item standing on <paramref name="basePos"/>, optionally scaled (carried items are smaller).</summary>
    public void Draw(Item item, Vector3 basePos, float scale = 1f, float yawDegrees = 0f)
    {
        Rlgl.PushMatrix();
        Rlgl.Translatef(basePos.X, basePos.Y, basePos.Z);
        if (yawDegrees != 0f) Rlgl.Rotatef(yawDegrees, 0, 1, 0);
        Rlgl.Scalef(scale, scale, scale);
        DrawAtOrigin(item);
        Rlgl.PopMatrix();
    }

    private void DrawAtOrigin(Item item)
    {
        var toy = content.FindToy(item.ToyId);
        var color = toy != null ? Palette.Hex(toy.Color) : Palette.Wood;

        if (item.Stage == ItemStage.Kit) { DrawKit(Vector3.Zero, item.ToyId); return; }

        if (item.IsCoal)
        {
            if (assets.TryGetModel(AssetLibrary.CoalModel, out var coal)) { Raylib.DrawModel(coal, Vector3.Zero, 1f, Color.White); return; }
            var dark = new Color(38, 38, 43, 255);
            Raylib.DrawSphere(new Vector3(-0.06f, 0.07f, 0), 0.08f, dark);
            Raylib.DrawSphere(new Vector3(0.07f, 0.06f, 0.03f), 0.07f, dark);
            Raylib.DrawSphere(new Vector3(0, 0.14f, -0.02f), 0.07f, dark);
            return;
        }

        if (item.IsSupply)
        {
            if (assets.TryGetModel(AssetLibrary.SupplyCrateModel, out var crate)) { Raylib.DrawModel(crate, Vector3.Zero, 1f, Color.White); return; }
            Raylib.DrawCube(new Vector3(0, 0.18f, 0), 0.44f, 0.36f, 0.38f, Palette.WoodDark);
            Raylib.DrawCube(new Vector3(0, 0.18f, 0), 0.06f, 0.37f, 0.39f, Palette.Pine); // strap: not a toy kit
            return;
        }

        if (item.Stage == ItemStage.Botched)
        {
            if (assets.TryGetModel(toy?.BotchedModel, out var botched)) { Raylib.DrawModel(botched, Vector3.Zero, 1f, Color.White); return; }
            // Grey-box: the toy shape knocked over and dulled.
            Rlgl.PushMatrix();
            Rlgl.Translatef(0, 0.15f, 0);
            Rlgl.Rotatef(70, 0, 0, 1);
            DrawToyShape(item.ToyId, Vector3.Zero, Palette.Shade(color, 0.55f));
            Rlgl.PopMatrix();
            return;
        }

        // A variant (big wheels, a big bear, a rocket pack) is drawn from its parts, as it was built on the bench.
        var variant = item.Variant != null ? toy?.Variant(item.Variant) : null;
        if (variant != null)
            DrawBuild(item.ToyId, variant.Order, Vector3.Zero);
        else if (toy?.Model != null && assets.TryGetModel(toy.Model, out var model))
            Raylib.DrawModel(model, Vector3.Zero, 1f, Color.White);
        else
            DrawToyShape(item.ToyId, Vector3.Zero, color);

        if (item.HasRibbon)
        {
            var (grow, lift) = toy != null && variant != null ? Shape(toy, variant.Order) : (1f, 0f);
            DrawRibbon(new Vector3(0, (ToyHeight(item.ToyId) + lift) * grow, 0), RibbonTint(item.RibbonColor));
        }
    }

    /// <summary>
    /// How the parts on so far change the toy: the scale from parts that make it grow (extra stuffing), and how far
    /// the parts that lift the others raise them (big wheels raise the boiler and cab).
    /// </summary>
    private static (float Grow, float Lift) Shape(Toy toy, IReadOnlyList<string> placed)
    {
        float grow = 1f, lift = 0f;
        foreach (var id in placed)
            if (toy.Part(id) is { } p)
            {
                if (p.Grow > 0) grow *= (float)p.Grow;
                lift += (float)p.Lift;
            }
        return (grow, lift);
    }

    /// <summary>
    /// A toy being built on the bench: the parts hammered on so far, each from its own model (they stack up into the
    /// finished toy). A part with a lift raises the others; one that grows the toy scales all of it; one that goes
    /// inside isn't drawn. Grey-box fallback: the toy shape growing taller part by part.
    /// </summary>
    public void DrawBuild(string toyId, IEnumerable<string> placedParts, Vector3 basePos)
    {
        var toy = content.FindToy(toyId);
        if (toy == null) return;
        var placed = placedParts.ToList();
        var (grow, _) = Shape(toy, placed);
        bool allModels = placed.All(id => toy.Part(id) is { } p && (p.Inside || assets.TryGetModel(p.Model, out _)));
        if (allModels)
        {
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < placed.Count; i++)
            {
                var part = toy.Part(placed[i])!;
                int copy = seen[part.Id] = seen.GetValueOrDefault(part.Id) + 1; // a second set of wheels is the carriage
                if (part.Inside || !assets.TryGetModel(ModelOf(part, copy), out var model)) continue;
                float lift = 0;
                for (int o = 0; o < placed.Count; o++) if (o != i) lift += (float)(toy.Part(placed[o])?.Lift ?? 0);
                Raylib.DrawModelEx(model, basePos + new Vector3(0, lift * grow, 0), Vector3.UnitY, 0, new Vector3(grow), Color.White);
            }
            return;
        }
        if (placed.Count == 0) return;
        float fraction = (float)placed.Count / Math.Max(1, toy.Standard.Order.Count);
        Rlgl.PushMatrix();
        Rlgl.Translatef(basePos.X, basePos.Y, basePos.Z);
        Rlgl.Scalef(grow, Math.Min(1, fraction) * grow, grow);
        DrawToyShape(toyId, Vector3.Zero, Palette.Hex(toy.Color));
        Rlgl.PopMatrix();
    }

    private readonly Dictionary<string, BoundingBox> _bounds = new();

    /// <summary>A part's model for its <paramref name="copy"/>-th use in a combination (its repeat model after the first).</summary>
    private static string? ModelOf(ToyPart part, int copy) => copy > 1 && part.RepeatModel != null ? part.RepeatModel : part.Model;

    /// <summary>
    /// One loose piece lying on the bench, waiting to go on: the part's model moved down onto <paramref name="p"/> and
    /// centred over it (part models are placed where they sit on the finished toy, often well off the floor).
    /// Grey-box fallback: a small block in the toy's colour.
    /// </summary>
    /// <param name="copy">Which copy of the part this is (1 = the first; a second set of wheels lies there as the carriage).</param>
    public void DrawPiece(string toyId, string partId, Vector3 p, float yawDegrees, float scale, int copy = 1)
    {
        var toy = content.FindToy(toyId);
        var part = toy?.Part(partId);
        string? file = part == null ? null : ModelOf(part, copy);
        if (file != null && assets.TryGetModel(file, out var model))
        {
            if (!_bounds.TryGetValue(file, out var box)) _bounds[file] = box = Raylib.GetModelBoundingBox(model);
            var centre = (box.Min + box.Max) / 2;
            Rlgl.PushMatrix();
            Rlgl.Translatef(p.X, p.Y, p.Z);
            Rlgl.Rotatef(yawDegrees, 0, 1, 0);
            Rlgl.Scalef(scale, scale, scale);
            Rlgl.Translatef(-centre.X, -box.Min.Y, -centre.Z);
            Raylib.DrawModel(model, Vector3.Zero, 1f, Color.White);
            Rlgl.PopMatrix();
            return;
        }
        var colour = toy != null ? Palette.Hex(toy.Color) : Palette.Wood;
        var c = p + new Vector3(0, 0.06f * scale, 0);
        Raylib.DrawCube(c, 0.16f * scale, 0.12f * scale, 0.16f * scale, colour);
        Raylib.DrawCubeWires(c, 0.16f * scale, 0.12f * scale, 0.16f * scale, Palette.Ink);
    }

    /// <summary>The hammer lying on its side, e.g. on the bench while nobody is using it.</summary>
    public void DrawHammerLying(Vector3 p, float yawDegrees)
    {
        if (!assets.TryGetModel(AssetLibrary.HammerModel, out var hammer)) return;
        // Tip the standing hammer over onto its side (handle along the bench), resting on the head's flat side.
        hammer.Transform = Raymath.MatrixMultiply(
            Raymath.MatrixMultiply(Raymath.MatrixRotateZ(MathF.PI / 2), Raymath.MatrixRotateY(yawDegrees * MathF.PI / 180f)),
            Raymath.MatrixTranslate(p.X, p.Y + 0.023f, p.Z));
        Raylib.DrawModel(hammer, Vector3.Zero, 1f, Color.White);
    }

    /// <summary>A standing tool (e.g. the shovel) tipped <paramref name="tiltDegrees"/> backwards, resting on its foot at p.</summary>
    public void DrawLeaning(string model, Vector3 p, float yawDegrees, float tiltDegrees)
    {
        if (!assets.TryGetModel(model, out var tool)) return;
        tool.Transform = Raymath.MatrixMultiply(
            Raymath.MatrixMultiply(Raymath.MatrixRotateX(-tiltDegrees * MathF.PI / 180f), Raymath.MatrixRotateY(yawDegrees * MathF.PI / 180f)),
            Raymath.MatrixTranslate(p.X, p.Y, p.Z));
        Raylib.DrawModel(tool, Vector3.Zero, 1f, Color.White);
    }

    /// <summary>A kit crate in the toy's colour, with the toy's picture pasted on all four sides.</summary>
    public void DrawKit(Vector3 p, string toyId)
    {
        var toy = content.FindToy(toyId);
        var toyColor = toy != null ? Palette.Hex(toy.Color) : Palette.Wood;
        const float s = 0.42f;
        if (assets.TryGetModel(AssetLibrary.KitModel, out var model)) Raylib.DrawModel(model, p, 1f, toyColor);
        else
        {
            var c = p + new Vector3(0, s / 2, 0);
            Raylib.DrawCube(c, s, s, s, Palette.Wood);
            Raylib.DrawCube(c, s + 0.02f, 0.12f, s + 0.02f, toyColor); // label band shows which toy
            Raylib.DrawCubeWires(c, s, s, s, Palette.Ink);
        }
        for (int side = 0; side < 4; side++) // over the model's front label, between the corner posts
        {
            float yaw = side * 90f;
            var outward = Raymath.Vector3RotateByAxisAngle(Vector3.UnitZ, Vector3.UnitY, yaw * MathF.PI / 180f);
            DrawPicture(toyId, p + new Vector3(0, s / 2, 0) + outward * (s / 2 + 0.028f), yaw, 0.24f);
        }
    }

    /// <summary>
    /// A toy's picture as a square card <paramref name="size"/> metres wide, centred at <paramref name="centre"/>, standing
    /// upright and facing <paramref name="yawDegrees"/> (0 = +Z). Lit like everything else. Skipped if there is no picture.
    /// </summary>
    public void DrawPicture(string toyId, Vector3 centre, float yawDegrees, float size)
    {
        if (!assets.TryGetPicture(toyId, out _, out var quad)) return;
        quad.Transform = Raymath.MatrixMultiply(
            Raymath.MatrixMultiply(Raymath.MatrixScale(size, 1, size), Raymath.MatrixRotateX(MathF.PI / 2)),
            Raymath.MatrixMultiply(Raymath.MatrixRotateY(yawDegrees * MathF.PI / 180f), Raymath.MatrixTranslate(centre.X, centre.Y, centre.Z)));
        Raylib.DrawModel(quad, Vector3.Zero, 1f, Color.White);
    }

    private static float ToyHeight(string toyId) => toyId switch { "train" => 0.42f, "bear" => 0.55f, "robot" => 0.62f, "boat" => 0.6f, _ => 0.4f };

    private static void DrawToyShape(string toyId, Vector3 p, Color color)
    {
        switch (toyId)
        {
            case "train":
                Raylib.DrawCube(p + new Vector3(-0.05f, 0.2f, 0), 0.5f, 0.2f, 0.26f, color);           // boiler
                Raylib.DrawCube(p + new Vector3(0.17f, 0.3f, 0), 0.18f, 0.24f, 0.28f, Palette.Ink);    // cab
                Raylib.DrawCylinder(p + new Vector3(-0.22f, 0.3f, 0), 0.05f, 0.06f, 0.14f, 8, Palette.Ink); // chimney
                foreach (float x in new[] { -0.2f, 0.0f, 0.18f })
                foreach (float z in new[] { -0.15f, 0.15f })
                    Raylib.DrawCylinderEx(p + new Vector3(x, 0.07f, z - 0.02f), p + new Vector3(x, 0.07f, z + 0.02f), 0.07f, 0.07f, 10, Palette.Gold);
                break;

            case "bear":
                Raylib.DrawSphere(p + new Vector3(0, 0.17f, 0), 0.17f, color);                          // body
                Raylib.DrawSphere(p + new Vector3(0, 0.4f, 0), 0.13f, color);                           // head
                Raylib.DrawSphere(p + new Vector3(-0.1f, 0.51f, 0), 0.05f, color);                      // ears
                Raylib.DrawSphere(p + new Vector3(0.1f, 0.51f, 0), 0.05f, color);
                Raylib.DrawSphere(p + new Vector3(0, 0.38f, 0.1f), 0.06f, Palette.Snow);                // contrasting face
                break;

            case "robot":
                Raylib.DrawCube(p + new Vector3(0, 0.16f, 0), 0.3f, 0.32f, 0.22f, color);               // body
                Raylib.DrawCube(p + new Vector3(0, 0.42f, 0), 0.22f, 0.18f, 0.2f, Palette.Shade(color, 0.8f)); // square head
                Raylib.DrawCubeWires(p + new Vector3(0, 0.42f, 0), 0.22f, 0.18f, 0.2f, Palette.Ink);
                Raylib.DrawCylinder(p + new Vector3(0, 0.51f, 0), 0.012f, 0.012f, 0.08f, 6, Palette.Ink); // antenna
                Raylib.DrawSphere(p + new Vector3(0, 0.6f, 0), 0.03f, Palette.Berry);
                break;

            case "boat":
                Raylib.DrawCube(p + new Vector3(0, 0.08f, 0), 0.5f, 0.14f, 0.2f, color);                  // hull
                Raylib.DrawCylinder(p + new Vector3(0, 0.15f, 0), 0.012f, 0.012f, 0.45f, 6, Palette.Ink);  // mast
                Raylib.DrawCube(p + new Vector3(0.09f, 0.36f, 0), 0.16f, 0.3f, 0.01f, Palette.Berry);      // sail
                break;

            default:
                Raylib.DrawCube(p + new Vector3(0, 0.2f, 0), 0.4f, 0.4f, 0.4f, color);
                break;
        }
    }

    /// <summary>The colour of a ribbon (tuning.json > ribbonColors), gold for a plain one.</summary>
    public Color RibbonTint(string? colourId) =>
        content.Tuning.FindRibbon(colourId) is { } r ? Palette.Hex(r.Color) : Palette.Gold;

    /// <summary>A spool of ribbon lying on its side, turned to <paramref name="yawDegrees"/>, tinted its colour.</summary>
    public void DrawSpool(Vector3 p, float yawDegrees, Color colour, float scale = 1f)
    {
        if (assets.TryGetModel(AssetLibrary.SpoolModel, out var spool))
        {
            Raylib.DrawModelEx(spool, p, Vector3.UnitY, yawDegrees, new Vector3(scale), colour);
            return;
        }
        var axis = Raymath.Vector3RotateByAxisAngle(Vector3.UnitX, Vector3.UnitY, yawDegrees * MathF.PI / 180f) * 0.045f * scale;
        var c = p + new Vector3(0, 0.06f * scale, 0);
        Raylib.DrawCylinderEx(c - axis, c + axis, 0.06f * scale, 0.06f * scale, 12, colour);
    }

    private void DrawRibbon(Vector3 top, Color colour)
    {
        if (assets.TryGetModel(AssetLibrary.RibbonModel, out var model)) { Raylib.DrawModel(model, top, 1f, colour); return; }
        Raylib.DrawSphere(top + new Vector3(-0.06f, 0.03f, 0), 0.05f, colour);
        Raylib.DrawSphere(top + new Vector3(0.06f, 0.03f, 0), 0.05f, colour);
        Raylib.DrawSphere(top + new Vector3(0, 0.03f, 0), 0.03f, Palette.Berry);
    }
}
