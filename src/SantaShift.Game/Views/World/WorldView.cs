using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Views.World;

/// <summary>Draws the whole 3D workshop: room, stations and elf. Reads state; never changes it.</summary>
public sealed class WorldView
{
    private readonly GameContent _content;
    private readonly WorldSpace _world;
    private readonly StationView _stations;
    private readonly PlayerView _player;

    public WorldView(GameContent content, IAssetLibrary assets, WorldSpace world)
    {
        _content = content;
        _world = world;
        var items = new ItemView(content, assets);
        _stations = new StationView(content, assets, world, items);
        _player = new PlayerView(assets, world, items);
    }

    public WorldSpace Space => _world;

    /// <summary>Draws the room only (used behind the title screen).</summary>
    public void DrawRoom()
    {
        Raylib.ClearBackground(Palette.Ink);
        Raylib.BeginMode3D(_world.Camera);
        DrawFloorAndWalls();
        foreach (var s in _content.Stations) _stations.Draw(s, selected: false);
        Raylib.EndMode3D();
    }

    public void Draw(Simulation sim, float facingDegrees, float bob)
    {
        Raylib.ClearBackground(Palette.Ink);
        Raylib.BeginMode3D(_world.Camera);
        DrawFloorAndWalls();

        var selected = sim.SelectedStation();
        foreach (var s in _content.Stations) _stations.Draw(s, s == selected);

        _player.Draw(sim.State, facingDegrees, bob);
        Raylib.EndMode3D();
    }

    /// <summary>Station names, drawn in the UI pass so they stay sharp.</summary>
    public void DrawLabels(UiCanvas ui, Station? selected)
    {
        foreach (var s in _content.Stations)
        {
            var at = _world.ToUi(_stations.LabelAnchor(s));
            float w = ui.MeasureText(s.Label, 14) + 12;
            var bg = s == selected ? Palette.Gold : Palette.WithAlpha(Palette.Ink, 200);
            var fg = s == selected ? Palette.Ink : Palette.Snow;
            ui.FillRounded(at.X - w / 2, at.Y - 20, w, 20, bg, 0.5f);
            ui.Text(s.Label, at.X, at.Y - 17, 14, fg, Align.Center);
        }
    }

    /// <summary>Where to draw things above the elf's head (progress bar), in UI coordinates.</summary>
    public Vector2 AbovePlayer(RunState state, float height = 1.95f) =>
        _world.ToUi(_world.ToWorld(state.PlayerPosition, height));

    private void DrawFloorAndWalls()
    {
        var f = _content.Tuning.Floor;
        var centre = _world.ToWorld(new Point2((f.Left + f.Right) / 2, (f.Top + f.Bottom) / 2));
        float w = _world.Metres(f.Right - f.Left), d = _world.Metres(f.Bottom - f.Top);

        Raylib.DrawPlane(centre, new Vector2(w, d), Palette.Floor);
        for (float z = -d / 2 + 0.5f; z < d / 2; z += 1f) // plank stripes
            Raylib.DrawPlane(centre + new Vector3(0, 0.002f, z), new Vector2(w, 0.5f), Palette.FloorStripe);

        float back = centre.Z - d / 2;
        Raylib.DrawCube(new Vector3(centre.X, 1.0f, back - 0.15f), w + 0.6f, 2.0f, 0.3f, Palette.Pine);         // back wall
        Raylib.DrawCube(new Vector3(centre.X, 1.4f, back - 0.0f), w + 0.6f, 0.25f, 0.05f, Palette.Berry);       // decorative strip
        Raylib.DrawCube(new Vector3(centre.X - w / 2 - 0.15f, 0.3f, centre.Z), 0.3f, 0.6f, d, Palette.Shade(Palette.Pine, 0.8f)); // side walls
        Raylib.DrawCube(new Vector3(centre.X + w / 2 + 0.15f, 0.3f, centre.Z), 0.3f, 0.6f, d, Palette.Shade(Palette.Pine, 0.8f));
    }
}
