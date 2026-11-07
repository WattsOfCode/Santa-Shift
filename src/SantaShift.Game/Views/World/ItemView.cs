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

        if (item.Stage == ItemStage.Kit) { DrawKit(Vector3.Zero, color); return; }

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

        if (toy?.Model != null && assets.TryGetModel(toy.Model, out var model))
            Raylib.DrawModel(model, Vector3.Zero, 1f, Color.White);
        else
            DrawToyShape(item.ToyId, Vector3.Zero, color);

        if (item.HasRibbon) DrawRibbon(new Vector3(0, ToyHeight(item.ToyId), 0), RibbonTint(item.RibbonColor));
    }

    /// <summary>
    /// A toy being built on the bench: the parts hammered on so far, each from its own model (they stack up into
    /// the finished toy). Grey-box fallback: the toy shape growing taller part by part.
    /// </summary>
    public void DrawBuild(string toyId, IEnumerable<string> placedParts, Vector3 basePos)
    {
        var toy = content.FindToy(toyId);
        if (toy == null) return;
        var placed = placedParts.ToList();
        bool allModels = toy.Parts.All(p => assets.TryGetModel(p.Model, out _));
        if (allModels)
        {
            foreach (var id in placed)
                if (assets.TryGetModel(toy.Parts.First(p => p.Id == id).Model, out var model))
                    Raylib.DrawModel(model, basePos, 1f, Color.White);
            return;
        }
        if (placed.Count == 0) return;
        float fraction = (float)placed.Count / Math.Max(1, toy.Parts.Count);
        Rlgl.PushMatrix();
        Rlgl.Translatef(basePos.X, basePos.Y, basePos.Z);
        Rlgl.Scalef(1, fraction, 1);
        DrawToyShape(toyId, Vector3.Zero, Palette.Hex(toy.Color));
        Rlgl.PopMatrix();
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

    public void DrawKit(Vector3 p, Color toyColor)
    {
        if (assets.TryGetModel(AssetLibrary.KitModel, out var model)) { Raylib.DrawModel(model, p, 1f, toyColor); return; }
        const float s = 0.42f;
        var c = p + new Vector3(0, s / 2, 0);
        Raylib.DrawCube(c, s, s, s, Palette.Wood);
        Raylib.DrawCube(c, s + 0.02f, 0.12f, s + 0.02f, toyColor); // label band shows which toy
        Raylib.DrawCubeWires(c, s, s, s, Palette.Ink);
    }

    private static float ToyHeight(string toyId) => toyId switch { "train" => 0.42f, "bear" => 0.55f, "robot" => 0.62f, _ => 0.4f };

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
