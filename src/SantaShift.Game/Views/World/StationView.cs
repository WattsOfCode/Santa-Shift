using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.World;

/// <summary>Draws one workshop fixture: its Blender model if present, otherwise a grey-box shape.</summary>
public sealed class StationView(GameContent content, IAssetLibrary assets, WorldSpace world, ItemView items)
{
    public static float HeightOf(StationType type) => type switch
    {
        StationType.Dispenser => 0.8f,
        StationType.Bench => 0.55f, // low elf-height bench, so the short elf can swing a hammer over it
        StationType.Ribbon => 0.85f,
        StationType.Shipping => 1.1f,
        StationType.Recycle => 0.75f,
        _ => 0.8f,
    };

    public void Draw(Station s, bool selected)
    {
        var centre = world.ToWorld(new Point2(s.Body.X, s.Body.Y));
        float w = world.Metres(s.Body.W), d = world.Metres(s.Body.H), h = HeightOf(s.Type);

        if (assets.TryGetModel(s.Model, out var model))
        {
            Raylib.DrawModelEx(model, centre, Vector3.UnitY, (float)s.Yaw, Vector3.One, Color.White);
        }
        else
        {
            DrawGreyBox(s, centre, w, h, d);
        }

        if (s.Type == StationType.Dispenser && s.ToyId != null)
        {
            var toy = content.FindToy(s.ToyId);
            items.DrawKit(centre + new Vector3(0, h, 0), toy != null ? Palette.Hex(toy.Color) : Palette.Wood);
        }

        if (selected)
        {
            var c = centre + new Vector3(0, h / 2, 0);
            Raylib.DrawCubeWires(c, w + 0.12f, h + 0.12f, d + 0.12f, Palette.Gold);
            Raylib.DrawCubeWires(c, w + 0.16f, h + 0.16f, d + 0.16f, Palette.Gold);
        }
    }

    /// <summary>
    /// The spot on the station's top in front of the elf: from where the elf stands, 25 cm short of the station's
    /// centre (at least 30 cm in). Toys are built here on the bench.
    /// </summary>
    public Vector3 WorkSpot(Station s)
    {
        var centre = world.ToWorld(new Point2(s.Body.X, s.Body.Y));
        var stand = world.ToWorld(s.Interact);
        var flat = new Vector3(centre.X - stand.X, 0, centre.Z - stand.Z);
        float distance = flat.Length();
        if (distance < 0.001f) return centre + new Vector3(0, HeightOf(s.Type), 0);
        var spot = stand + flat / distance * Math.Max(distance - 0.25f, 0.3f);
        return new Vector3(spot.X, HeightOf(s.Type), spot.Z);
    }

    /// <summary>Unit vector along the floor from where the elf stands towards the work spot.</summary>
    public Vector3 WorkDirection(Station s)
    {
        var spot = WorkSpot(s);
        var stand = world.ToWorld(s.Interact);
        var flat = new Vector3(spot.X - stand.X, 0, spot.Z - stand.Z);
        return flat.LengthSquared() < 1e-6f ? -Vector3.UnitZ : Vector3.Normalize(flat);
    }

    /// <summary>Where the station's label sits, in the 3D world.</summary>
    public Vector3 LabelAnchor(Station s) =>
        world.ToWorld(new Point2(s.Body.X, s.Body.Y), HeightOf(s.Type) + (s.Type == StationType.Dispenser ? 0.55f : 0.15f));

    private static void DrawGreyBox(Station s, Vector3 c, float w, float h, float d)
    {
        var mid = c + new Vector3(0, h / 2, 0);
        switch (s.Type)
        {
            case StationType.Dispenser:
                Raylib.DrawCube(mid, w, h, d, Palette.Wood);
                Raylib.DrawCube(c + new Vector3(0, h - 0.04f, 0), w + 0.04f, 0.08f, d + 0.04f, Palette.WoodDark);
                break;
            case StationType.Bench:
                Raylib.DrawCube(c + new Vector3(0, h - 0.06f, 0), w, 0.12f, d, Palette.Wood);        // top
                Raylib.DrawCube(c + new Vector3(0, (h - 0.12f) / 2, 0), w - 0.2f, h - 0.12f, d - 0.2f, Palette.WoodDark);
                break;
            case StationType.Ribbon:
                Raylib.DrawCube(mid, w, h, d, Palette.Pine);
                Raylib.DrawCylinderEx(c + new Vector3(-0.3f, h + 0.12f, 0), c + new Vector3(0.3f, h + 0.12f, 0), 0.12f, 0.12f, 12, Palette.Gold);
                break;
            case StationType.Shipping:
                Raylib.DrawCube(mid, w, h, d, Palette.Ice);
                Raylib.DrawCube(c + new Vector3(-w / 2 + 0.02f, h * 0.55f, 0), 0.06f, h * 0.5f, d * 0.6f, Palette.Ink); // chute mouth
                break;
            case StationType.Recycle:
                Raylib.DrawCylinder(c, w / 2, w / 2 * 0.85f, h, 14, Palette.Pine);
                Raylib.DrawCylinder(c + new Vector3(0, h, 0), w / 2 + 0.03f, w / 2 + 0.03f, 0.05f, 14, Palette.Shade(Palette.Pine, 0.7f));
                break;
        }
        Raylib.DrawCubeWires(mid, w, h, d, Palette.WithAlpha(Palette.Ink, 120));
    }
}
