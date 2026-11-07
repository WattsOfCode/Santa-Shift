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
        _stations = new StationView(assets, world, _items);
        _player = new PlayerView(assets, world, _items);
    }

    /// <summary>Glows and window snow, shared by every screen's view (one small texture).</summary>
    private static WorldEffects? _effects;
    private WorldEffects Effects => _effects ??= new WorldEffects(_content);

    public WorldSpace Space => _world;

    /// <summary>
    /// Where to draw the elf this frame, between its last two simulation steps (set by the workshop each frame); null =
    /// exactly where the rules have it.
    /// </summary>
    public Point2? PlayerAt { get; set; }

    private Point2 ElfAt(RunState state) => PlayerAt ?? state.PlayerPosition;

    /// <summary>The next station in the guided first round (the arrows lead there), or null for no arrows.</summary>
    public Station? GuideTarget { get; set; }

    private GuideArrowView? _guide;

    /// <summary>The player elf's hat colour (an unlock), or null for red. The workshop elves always wear red.</summary>
    public Color? PlayerHat { get => _player.Hat; set => _player.Hat = value; }

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
            if (s.Type == StationType.Coal) DrawShovelRest(s);
            if (s.Type == StationType.Ribbon) DrawRibbonTop(s, null);
        }
        DrawNpcs();
        Effects.Draw(_world, Raylib.GetTime());
        Raylib.EndMode3D();
    }

    public void Draw(Simulation sim, float facingDegrees, float bob, ElfPose pose)
    {
        Raylib.ClearBackground(Palette.Ink);
        Raylib.BeginMode3D(_world.Camera);
        DrawRoomShell();

        var state = sim.State;
        var selected = state.IsWorking ? null : sim.SelectedStation(); // no outline in the way of the work
        using (App.FrameProfiler.Time("stations"))
            foreach (var s in _content.Stations)
            {
                _stations.Draw(s, s == selected, s.Type == StationType.Dispenser && s.ToyId != null ? state.KitsLeft(s.ToyId) : null);
                if (s.Type == StationType.Bench)
                    DrawBenchTop(s, state.Assembly?.StationId == s.Id ? state.Assembly : null, state.CarriedItem);
                if (s.Type == StationType.Coal && pose.Activity != ElfActivity.Shovelling) DrawShovelRest(s);
                if (s.Type == StationType.Ribbon) DrawRibbonTop(s, state);
            }

        using (App.FrameProfiler.Time("npcs")) DrawNpcs();
        using (App.FrameProfiler.Time("player")) _player.Draw(state, ElfAt(state), facingDegrees, bob, pose);
        if (GuideTarget != null)
            (_guide ??= new GuideArrowView(_content, _assets, _world)).Draw(ElfAt(state), GuideTarget, Raylib.GetTime(), WorldEffects.ReducedMotion);
        using (App.FrameProfiler.Time("effects")) Effects.Draw(_world, Raylib.GetTime());
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
            _items.DrawKit(spot - along * 0.65f + dir * 0.1f, game.ToyId);
            _items.DrawBuild(game.ToyId, game.Order.Take(game.Placed), spot);
            return;
        }
        float yaw = MathF.Atan2(along.X, along.Z) * 180f / MathF.PI;
        _items.DrawHammerLying(spot + along * 0.55f + dir * 0.05f, yaw + 90);
    }

    /// <summary>The spool under the mouse while choosing a ribbon (-1 for none); set by the workshop controller.</summary>
    public int HoveredSpool { get; set; } = -1;

    /// <summary>
    /// The ribbon station's top: a spool of each ribbon colour. While the elf chooses or ties a ribbon there, the toy
    /// sits in front of the spools; the spool under the mouse lifts, and while tying a strand runs to the toy.
    /// </summary>
    private void DrawRibbonTop(Station s, RunState? state)
    {
        var colours = _content.Tuning.RibbonColors;
        if (colours.Length == 0) return;
        var dir = _stations.WorkDirection(s);
        float spoolYaw = MathF.Atan2(-dir.X, -dir.Z) * 180f / MathF.PI;
        bool picking = state?.RibbonPick?.StationId == s.Id;
        var tying = state?.CraftingAction is { Type: CraftingType.Ribbon } a && a.StationId == s.Id ? a : null;

        var toyAt = _stations.WorkSpot(s) + dir * 0.22f; // behind the row of spools
        for (int i = 0; i < colours.Length; i++)
        {
            var p = _stations.SpoolSpot(s, i, colours.Length);
            bool hot = picking && i == HoveredSpool;
            bool used = tying?.RibbonColor == colours[i].Id;
            float lift = hot ? 0.08f : used ? 0.04f : 0f;
            _items.DrawSpool(p + new Vector3(0, lift, 0), spoolYaw, Palette.Hex(colours[i].Color), hot ? 1.9f : 1.5f);
            if (used && state!.CarriedItem != null) // the ribbon running from the spool to the toy
                Raylib.DrawCylinderEx(p + new Vector3(0, 0.14f, 0), toyAt + new Vector3(0, 0.22f, 0), 0.008f, 0.008f, 4,
                    Palette.Hex(colours[i].Color));
        }
        if ((picking || tying != null) && state!.CarriedItem is { } item)
            _items.Draw(item, toyAt, 1f, MathF.Atan2(dir.X, dir.Z) * 180f / MathF.PI);
    }

    /// <summary>Where spool <paramref name="index"/> is on screen (UI coordinates), or null when it is out of view.</summary>
    public Vector2? SpoolOnScreen(Station s, int index)
    {
        int n = _content.Tuning.RibbonColors.Length;
        var p = _stations.SpoolSpot(s, index, n) + new Vector3(0, 0.09f, 0);
        return _world.InFront(p) ? _world.ToUi(p) : null;
    }

    /// <summary>
    /// While choosing a ribbon: one label per spool ([key] colour name), in a single row above the spools so they never
    /// overlap however the camera sees them, each with a thin line down to its spool. The hovered one is gold.
    /// </summary>
    public void DrawSpoolLabels(UiCanvas ui, Station s, Func<int, string> keyFor)
    {
        const float size = 18, h = 28, gap = 8;
        var colours = _content.Tuning.RibbonColors;
        var labels = new List<(int Index, Vector2 At, float KeyW, float W)>();
        for (int i = 0; i < colours.Length; i++)
        {
            if (SpoolOnScreen(s, i) is not { } at) continue;
            float kw = Math.Max(24, ui.MeasureText(keyFor(i), size) + 12);
            labels.Add((i, at, kw, 4 + kw + 8 + 10 + 6 + ui.MeasureText(colours[i].Name, size) + 12));
        }
        if (labels.Count == 0) return;

        // Left to right as on screen, pushed apart where they would touch, then the row re-centred over the spools.
        labels.Sort((a, b) => a.At.X.CompareTo(b.At.X));
        var lefts = new float[labels.Count];
        for (int k = 0; k < labels.Count; k++)
        {
            lefts[k] = labels[k].At.X - labels[k].W / 2;
            if (k > 0) lefts[k] = MathF.Max(lefts[k], lefts[k - 1] + labels[k - 1].W + gap);
        }
        float wanted = labels.Average(l => l.At.X), actual = 0;
        for (int k = 0; k < labels.Count; k++) actual += lefts[k] + labels[k].W / 2;
        float shift = wanted - actual / labels.Count;
        float rowY = labels.Min(l => l.At.Y) - 72;

        for (int k = 0; k < labels.Count; k++)
        {
            var (i, at, kw, w) = labels[k];
            bool hot = i == HoveredSpool;
            float left = lefts[k] + shift, y = rowY - (hot ? 6 : 0);
            ui.Line(left + w / 2, y + h, at.X, at.Y - 8, 2, Palette.WithAlpha(hot ? Palette.Gold : Palette.Snow, 170));
            ui.FillRounded(left, y, w, h, hot ? Palette.Gold : Palette.WithAlpha(Palette.Ink, 230), 0.5f);
            ui.FillRounded(left + 4, y + 3, kw, h - 6, hot ? Palette.Ink : Palette.Gold, 0.4f);
            ui.Text(keyFor(i), left + 4 + kw / 2, y + 5, size, hot ? Palette.Gold : Palette.Ink, Align.Center);
            ui.FillRounded(left + 4 + kw + 8, y + 7, 10, h - 14, Palette.Hex(colours[i].Color), 0.5f);
            ui.Text(colours[i].Name, left + 4 + kw + 8 + 10 + 6, y + 5, size, hot ? Palette.Ink : Palette.Snow);
        }
    }

    /// <summary>
    /// Station names for nearby stations in view, drawn in the UI pass so they stay sharp. With limited kit bins, each
    /// bin shows how many kits are left. A bin that is empty while a letter still needs its toy (more orders than the elf
    /// is holding) is labelled in red from anywhere in the room: time to fetch a supply crate.
    /// </summary>
    public void DrawLabels(UiCanvas ui, Station? selected, RunState? state = null)
    {
        var d = _content.Dialogue;
        foreach (var s in _content.Stations)
        {
            var anchor = _stations.LabelAnchor(s);
            if (!_world.InFront(anchor)) continue;
            int? left = s.Type == StationType.Dispenser && s.ToyId != null ? state?.KitsLeft(s.ToyId) : null;
            bool empty = left is <= 0 && state != null && Short(state, s.ToyId!);
            if (s != selected && !empty && _world.DistanceFromCamera(anchor) > LabelRange) continue;

            var at = _world.ToUi(anchor);
            string label = left is { } n ? $"{s.Label}  {(empty ? d.Message("bin_stock_empty") : d.Message("bin_stock", n))}" : s.Label;
            float w = ui.MeasureText(label, 14) + 12;
            var bg = s == selected ? Palette.Gold : empty ? Palette.Berry : Palette.WithAlpha(Palette.Ink, 200);
            var fg = s == selected ? Palette.Ink : Palette.Snow;
            ui.FillRounded(at.X - w / 2, at.Y - 20, w, 20, bg, 0.5f);
            ui.Text(label, at.X, at.Y - 17, 14, fg, Align.Center);
        }
    }

    /// <summary>True when more nice children are waiting for this toy than the elf has in hand.</summary>
    private static bool Short(RunState state, string toyId) =>
        state.LiveOrders.Count(o => !o.Naughty && o.ToyId == toyId) > (state.CarriedItem?.ToyId == toyId ? 1 : 0);

    /// <summary>Where to draw things above the elf's head (progress bar), in UI coordinates. Null if off-screen.</summary>
    public Vector2? AbovePlayer(RunState state, float height = 1.95f)
    {
        var p = _world.ToWorld(ElfAt(state), height);
        return _world.InFront(p) ? _world.ToUi(p) : null;
    }

    /// <summary>
    /// Furniture and decorations, each from its own model (missing models are simply skipped). A prop whose model has
    /// an animation loops it (the fireplace's flames burn), at the 60 key frames per second the export bakes.
    /// </summary>
    private void DrawProps()
    {
        double time = Raylib.GetTime();
        foreach (var prop in _content.Props)
        {
            string file = AssetLibrary.PropFile(prop.Model);
            if (!_assets.TryGetModel(file, out var model)) continue;
            var anims = _assets.Animations(file);
            if (anims.Count > 0 && anims[0].KeyFrameCount > 1)
                using (App.FrameProfiler.Time("prop anim"))
                    SkeletonPoser.Pose(model, anims[0], (float)(time * 60 % (anims[0].KeyFrameCount - 1)));
            float s = (float)prop.Scale;
            Raylib.DrawModelEx(model, _world.ToWorld(prop.Position), Vector3.UnitY, (float)prop.Yaw, new Vector3(s), Color.White);
        }
    }

    /// <summary>
    /// The workshop elves at their own benches: the player's elf model, each hammering away on its own rhythm
    /// (a swing every 0.9 to 1.4 s) so they never move in step.
    /// </summary>
    private void DrawNpcs()
    {
        float dt = Raylib.GetFrameTime();
        for (int i = 0; i < _content.Npcs.Count; i++)
        {
            var npc = _content.Npcs[i];
            if (i >= _npcFigures.Count) { _npcFigures.Add(new ElfFigure(_assets)); _npcSwings.Add(0); _npcClocks.Add(-0.37f * i); }
            _npcClocks[i] += dt;
            float every = 0.9f + 0.5f * ((i * 0.618f) % 1);
            if (_npcClocks[i] >= every) { _npcClocks[i] -= every; _npcSwings[i]++; }

            var at = _world.ToWorld(npc.Position);
            Raylib.DrawCylinder(at + new Vector3(0, 0.012f, 0), 0.3f, 0.3f, 0.01f, 16, Palette.Shadow);
            var pose = new ElfPose(false, ElfActivity.Hammering, _npcSwings[i]);
            if (!_npcFigures[i].Draw(at, (float)npc.Yaw, pose, dt))
            {
                Rlgl.PushMatrix();
                Rlgl.Translatef(at.X, at.Y, at.Z);
                Rlgl.Rotatef((float)npc.Yaw, 0, 1, 0);
                PlayerView.DrawGreyBoxElf();
                Rlgl.PopMatrix();
            }
        }
    }

    private readonly List<ElfFigure> _npcFigures = [];
    private readonly List<int> _npcSwings = [];
    private readonly List<float> _npcClocks = [];

    /// <summary>The shovel leans against the coal scuttle while nobody is using it.</summary>
    private void DrawShovelRest(Station scuttle)
    {
        var centre = _world.ToWorld(new Point2(scuttle.Body.X, scuttle.Body.Y));
        var dir = _stations.WorkDirection(scuttle);
        var side = new Vector3(-dir.Z, 0, dir.X);
        float yaw = MathF.Atan2(dir.X, dir.Z) * 180f / MathF.PI;
        _items.DrawLeaning(AssetLibrary.ShovelModel, centre + side * 0.3f - dir * 0.05f, yaw, 18);
    }

    private void DrawRoomShell()
    {
        using (App.FrameProfiler.Time("props")) DrawProps();
        if (_assets.TryGetModel(AssetLibrary.RoomModel, out var room))
        {
            using (App.FrameProfiler.Time("room")) Raylib.DrawModel(room, Vector3.Zero, 1f, Color.White);
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
