using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Views.World;

/// <summary>
/// Draws the whole 3D workshop: room, stations and elf. Reads state; never changes it.
/// The room is room.glb from Blender when present; otherwise grey-box floor, walls and obstacles.
/// </summary>
public sealed class WorldView
{
    private const float WallHeight = 3.0f;
    private const float LabelRange = 9f; // metres; farther stations aren't labelled

    private readonly GameContent _content;
    private readonly IAssetLibrary _assets;
    private readonly WorldSpace _world;
    private readonly StationView _stations;
    private readonly PlayerView _player;
    private readonly ItemView _items;

    public WorldView(GameContent content, IAssetLibrary assets, WorldSpace world)
    {
        _content = content;
        _assets = assets;
        _world = world;
        _items = new ItemView(content, assets);
        _stations = new StationView(content, assets, world, _items);
        _player = new PlayerView(assets, world, _items);
    }

    public WorldSpace Space => _world;

    /// <summary>Where toys are built on a station's top (3D world), and the direction from the elf towards it.</summary>
    public Vector3 WorkSpot(Station s) => _stations.WorkSpot(s);
    public Vector3 WorkDirection(Station s) => _stations.WorkDirection(s);

    /// <summary>Draws the room and stations without the elf (behind menus).</summary>
    public void DrawRoom()
    {
        Raylib.ClearBackground(Palette.Ink);
        Raylib.BeginMode3D(_world.Camera);
        DrawRoomShell();
        foreach (var s in _content.Stations)
        {
            _stations.Draw(s, selected: false);
            if (s.Type == StationType.Bench) DrawBenchTop(s, null, null);
        }
        Raylib.EndMode3D();
    }

    public void Draw(Simulation sim, float facingDegrees, float bob, ElfPose pose)
    {
        Raylib.ClearBackground(Palette.Ink);
        Raylib.BeginMode3D(_world.Camera);
        DrawRoomShell();

        var state = sim.State;
        var selected = state.IsWorking ? null : sim.SelectedStation(); // no outline in the way of the work
        foreach (var s in _content.Stations)
        {
            _stations.Draw(s, s == selected);
            if (s.Type == StationType.Bench)
                DrawBenchTop(s, state.Assembly?.StationId == s.Id ? state.Assembly : null, state.CarriedItem);
        }

        _player.Draw(state, facingDegrees, bob, pose);
        Raylib.EndMode3D();
    }

    /// <summary>
    /// While a toy is built here: its kit crate and the parts hammered on so far. Otherwise the hammer lies on the bench
    /// (the elf picks it up while building).
    /// </summary>
    private void DrawBenchTop(Station bench, AssemblyState? game, Item? item)
    {
        var spot = _stations.WorkSpot(bench);
        var dir = _stations.WorkDirection(bench);
        var along = new Vector3(-dir.Z, 0, dir.X); // along the bench, to the elf's right
        if (game != null && item != null)
        {
            var toy = _content.FindToy(game.ToyId);
            _items.DrawKit(spot - along * 0.42f + dir * 0.1f, toy != null ? Palette.Hex(toy.Color) : Palette.Wood);
            _items.DrawBuild(game.ToyId, game.Order.Take(game.Placed), spot);
            return;
        }
        float yaw = MathF.Atan2(along.X, along.Z) * 180f / MathF.PI;
        _items.DrawHammerLying(spot + along * 0.55f + dir * 0.05f, yaw + 90);
    }

    /// <summary>Station names for nearby stations in view, drawn in the UI pass so they stay sharp.</summary>
    public void DrawLabels(UiCanvas ui, Station? selected)
    {
        foreach (var s in _content.Stations)
        {
            var anchor = _stations.LabelAnchor(s);
            if (!_world.InFront(anchor)) continue;
            if (s != selected && _world.DistanceFromCamera(anchor) > LabelRange) continue;

            var at = _world.ToUi(anchor);
            float w = ui.MeasureText(s.Label, 14) + 12;
            var bg = s == selected ? Palette.Gold : Palette.WithAlpha(Palette.Ink, 200);
            var fg = s == selected ? Palette.Ink : Palette.Snow;
            ui.FillRounded(at.X - w / 2, at.Y - 20, w, 20, bg, 0.5f);
            ui.Text(s.Label, at.X, at.Y - 17, 14, fg, Align.Center);
        }
    }

    /// <summary>Where to draw things above the elf's head (progress bar), in UI coordinates. Null if off-screen.</summary>
    public Vector2? AbovePlayer(RunState state, float height = 1.95f)
    {
        var p = _world.ToWorld(state.PlayerPosition, height);
        return _world.InFront(p) ? _world.ToUi(p) : null;
    }

    /// <summary>Furniture and decorations, each from its own model. Missing models are simply skipped.</summary>
    private void DrawProps()
    {
        foreach (var prop in _content.Props)
        {
            if (!_assets.TryGetModel(AssetLibrary.PropFile(prop.Model), out var model)) continue;
            float s = (float)prop.Scale;
            Raylib.DrawModelEx(model, _world.ToWorld(prop.Position), Vector3.UnitY, (float)prop.Yaw, new Vector3(s), Color.White);
        }
    }

    private void DrawRoomShell()
    {
        DrawProps();
        if (_assets.TryGetModel(AssetLibrary.RoomModel, out var room))
        {
            Raylib.DrawModel(room, Vector3.Zero, 1f, Color.White);
            return;
        }

        var f = _content.Floor;
        var centre = _world.ToWorld(new Point2((f.Left + f.Right) / 2, (f.Top + f.Bottom) / 2));
        float w = _world.Metres(f.Right - f.Left), d = _world.Metres(f.Bottom - f.Top);

        Raylib.DrawPlane(centre, new Vector2(w, d), Palette.Floor);
        for (float z = -d / 2 + 0.5f; z < d / 2; z += 1f) // plank stripes
            Raylib.DrawPlane(centre + new Vector3(0, 0.002f, z), new Vector2(w, 0.5f), Palette.FloorStripe);

        const float t = 0.3f, h = WallHeight;
        var wall = Palette.Pine;
        var side = Palette.Shade(Palette.Pine, 0.8f);
        Raylib.DrawCube(new Vector3(centre.X, h / 2, centre.Z - d / 2 - t / 2), w + 2 * t, h, t, wall);                // back
        Raylib.DrawCube(new Vector3(centre.X, h / 2, centre.Z + d / 2 + t / 2), w + 2 * t, h, t, Palette.Shade(wall, 0.9f)); // front
        Raylib.DrawCube(new Vector3(centre.X - w / 2 - t / 2, h / 2, centre.Z), t, h, d, side);                        // left
        Raylib.DrawCube(new Vector3(centre.X + w / 2 + t / 2, h / 2, centre.Z), t, h, d, side);                        // right
        Raylib.DrawCube(new Vector3(centre.X, 1.4f, centre.Z - d / 2 + 0.01f), w, 0.25f, 0.02f, Palette.Berry);        // decorative strip

        foreach (var c in _content.Colliders) // obstacles from a room layout, when there is no room model
        {
            var p = _world.ToWorld(new Point2(c.X, c.Y), 0.5f);
            Raylib.DrawCube(p, _world.Metres(c.W), 1f, _world.Metres(c.H), Palette.WoodDark);
            Raylib.DrawCubeWires(p, _world.Metres(c.W), 1f, _world.Metres(c.H), Palette.Ink);
        }
    }
}
