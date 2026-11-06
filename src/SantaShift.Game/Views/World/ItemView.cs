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
    public void Draw(Item item, Vector3 basePos, float scale = 1f)
    {
        Rlgl.PushMatrix();
        Rlgl.Translatef(basePos.X, basePos.Y, basePos.Z);
        Rlgl.Scalef(scale, scale, scale);
        DrawAtOrigin(item);
        Rlgl.PopMatrix();
    }

    private void DrawAtOrigin(Item item)
    {
        var toy = content.FindToy(item.ToyId);
        var color = toy != null ? Palette.Hex(toy.Color) : Palette.Wood;

        if (item.Stage == ItemStage.Kit) { DrawKit(Vector3.Zero, color); return; }

        if (toy?.Model != null && assets.TryGetModel(toy.Model, out var model))
            Raylib.DrawModel(model, Vector3.Zero, 1f, Color.White);
        else
            DrawToyShape(item.ToyId, Vector3.Zero, color);

        if (item.HasRibbon) DrawRibbon(new Vector3(0, ToyHeight(item.ToyId), 0));
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

    private void DrawRibbon(Vector3 top)
    {
        if (assets.TryGetModel(AssetLibrary.RibbonModel, out var model)) { Raylib.DrawModel(model, top, 1f, Color.White); return; }
        Raylib.DrawSphere(top + new Vector3(-0.06f, 0.03f, 0), 0.05f, Palette.Gold);
        Raylib.DrawSphere(top + new Vector3(0.06f, 0.03f, 0), 0.05f, Palette.Gold);
        Raylib.DrawSphere(top + new Vector3(0, 0.03f, 0), 0.03f, Palette.Berry);
    }
}
