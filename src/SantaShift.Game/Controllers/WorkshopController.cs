using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>The gameplay screen: runs one round (shift) of a run and coordinates its views.</summary>
public sealed class WorkshopController : IScreen
{
    private readonly GameServices _s;
    private readonly RunProgress _run;
    private readonly Simulation _sim;
    private readonly PlayerController _player;
    private readonly InteractionController _interaction;
    private readonly PauseController _pause;
    private readonly CameraController _camera;
    private readonly WorldView _world;
    private readonly HudView _hud;

    /// <summary>Starts the current round of <paramref name="run"/>.</summary>
    public WorkshopController(GameServices services, RunProgress run)
    {
        _s = services;
        _run = run;
        _sim = new Simulation(services.Content, run.Mode, run.Round, naughty: run.List.NaughtyChildren,
            modifiers: run.Modifiers, spirit: run.SpiritForNextRound(services.Content.Tuning.Spirit),
            tutorial: run.Round == 1 && GuideOn(services), cartParts: DebugCartParts ?? run.CartParts);
        _guideWasActive = TutorialGuide.Active(_sim);
        _player = new PlayerController(services.Input, services.Settings, services.Render.Movement);
        _interaction = new InteractionController(services.Content, services.Input, services.Audio);
        _interaction.Attach(_sim);
        _pause = new PauseController(services, _sim,
            restart: () => services.Scenes.Switch(NewRun(services, run.Mode)),
            quitToTitle: () => services.Scenes.Switch(new TitleController(services)));
        _camera = new CameraController(services.Render.ChaseCamera, services.Render.ShoulderCamera, services.World, services.Content);
        _world = new WorldView(services.Content, services.Assets, services.World);
        _world.PlayerHat = DebugHat != null ? Views.Palette.Hex(DebugHat) : services.HatColour;
        _hud = new HudView(services.Content, services.Input);
        _firePixels = FirePosition(services.Content);
        _mouse = new MenuMouse(services.Input);
    }

    /// <summary>Starts a brand-new run at round 1: Santa draws a fresh naughty and nice list and shows it first.</summary>
    public static IScreen NewRun(GameServices services, GameMode mode) => new NaughtyNiceController(services, NewRunProgress(services, mode));

    /// <summary>A fresh run with its naughty and nice list (nobody is naughty if the room has no coal station).</summary>
    public static RunProgress NewRunProgress(GameServices services, GameMode mode)
    {
        var content = services.Content;
        var run = DebugNaughty is { } names
            ? new RunProgress(mode, new NaughtyNiceList(content.Letters.GroupBy(l => l.Child).Select(g => g.First())
                .Select(l => new ListEntry(l.Child, l.Age, l.ToyId, names.Contains(l.Child)))))
            : RunProgress.Start(mode, content.Letters, content.HasCoal ? content.Tuning.NaughtyPerRun : 0, Random.Shared.Next());
        for (int r = 1; r < DebugRound; r++) // --round N: as if the rounds before it were cleared
        {
            int quota = content.Tuning.QuotaFor(mode, r);
            run.CompleteRound(new RunState { Delivered = quota, Score = quota * 100 }, quota, content.Tuning.Cart);
            run.StartNextRound();
        }
        return run;
    }

    /// <summary>Debug builds only (--round N): start the run at round N.</summary>
    public static int DebugRound = 1;

    /// <summary>Debug builds only (--skip S): fast-forward the shift S seconds with no input, for screenshots.</summary>
    public static double DebugSkip;

    /// <summary>The upbeat toy factory jingle while the shift runs.</summary>
    public string? Music => MusicIds.Workshop;

    /// <summary>
    /// The hearth crackles louder as the elf comes near: full within 1.5 m, fading to a faint murmur across the room,
    /// and half as loud while the pause menu is up.
    /// </summary>
    public float FireVolume
    {
        get
        {
            if (_firePixels is not { } fire) return 0f;
            var at = _sim.State.PlayerPosition;
            double metres = Math.Sqrt(Math.Pow(at.X - fire.X, 2) + Math.Pow(at.Y - fire.Y, 2)) / _sim.Tuning.PixelsPerMetre;
            float near = (float)Math.Clamp(1 - (metres - 1.5) / 9, 0, 1);
            float volume = 0.12f + 0.88f * near * near;
            return _pause.Blocking && _pause.State != PauseState.Ready ? volume * 0.5f : volume;
        }
    }

    /// <summary>Where the crackle comes from: the fireplace's light, else the coal scuttle on its hearth, else nowhere.</summary>
    private static Point2? FirePosition(GameContent content)
    {
        if (content.Lights.FirstOrDefault(l => l.Name.Contains("fireplace", StringComparison.OrdinalIgnoreCase)) is { } light)
            return content.Tuning.MetresToPixels(light.X, light.Z);
        return content.Stations.FirstOrDefault(st => st.Type == StationType.Coal)?.Interact;
    }

    private readonly Point2? _firePixels;
    private readonly MenuMouse _mouse;

    public Simulation Simulation => _sim;

    /// <summary>Debug builds only (--carry toy[:assembled][:ribbon]): start holding an item, for screenshots.</summary>
    public static Item? DebugCarry;

    /// <summary>Debug builds only (--assemble N): start at the bench building a train with N parts on.</summary>
    public static int? DebugAssembleParts;

    /// <summary>Debug builds only (--cart N): start with N toy cart parts built, whatever the round.</summary>
    public static int? DebugCartParts;

    /// <summary>Debug builds only (--cart-load train,bear): these finished toys start in the cart.</summary>
    public static string[]? DebugCartLoad;

    /// <summary>Debug builds only (--assemble N toy[:variant]): build this toy (following this variant) instead.</summary>
    public static string? DebugAssembleToy, DebugAssembleVariant;

    /// <summary>Debug builds only (--swing-every N): swing the hammer every N frames, for screenshots of the swing.</summary>
    public static int? DebugSwingEvery;

    /// <summary>Debug builds only (--naughty A,B): these children are naughty instead of a random pick.</summary>
    public static string[]? DebugNaughty;

    /// <summary>Debug builds only (--at stationId): start standing at that station, facing it.</summary>
    public static string? DebugAt;

    /// <summary>Debug builds only (--guide): guide round 1 whatever the settings and progress say.</summary>
    public static bool DebugGuide;

    private bool _guideWasActive;

    /// <summary>Whether round 1 is guided: Settings > Game > Tutorial helpers (on unless the player turned them off).</summary>
    private static bool GuideOn(GameServices s) => DebugGuide || s.Settings.TutorialHelpers;

    /// <summary>Debug builds only (--hat #RRGGBB): wear this hat colour, for screenshots.</summary>
    public static string? DebugHat;

    /// <summary>Debug builds only (--spool N): when choosing a ribbon, take spool N.</summary>
    public static int? DebugSpool;

    /// <summary>Debug builds only (--pause): open the pause menu once READY is over.</summary>
    public static bool DebugPause;

    /// <summary>Debug builds only (--use, with --at): press interact there once at the start.</summary>
    public static bool DebugUse;
    private bool _debugUsePending;

    /// <summary>Counts hammer swings (one per number key pressed while building); drives the swing animation.</summary>
    private int _swings;
    private int _frames;

    public void Enter()
    {
        for (long i = 0, n = _sim.Tuning.Ticks(DebugSkip); i < n; i++) _sim.Step(PlayerCommand.None);
        _sim.DrainEvents();
        if (DebugCarry != null) _sim.State.CarriedItem = DebugCarry;
        if (DebugAssembleParts is { } parts) DebugStartAssembly(parts);
        if (DebugAt != null && DebugAt.Split(',') is [var mx, var mz]
            && double.TryParse(mx, System.Globalization.CultureInfo.InvariantCulture, out double x)
            && double.TryParse(mz, System.Globalization.CultureInfo.InvariantCulture, out double z))
        {
            _sim.PlacePlayer(_s.Content.Tuning.MetresToPixels(x, z)); // --at x,z in metres
            _debugUsePending = DebugUse;
        }
        else if (DebugAt != null && _s.Content.Stations.FirstOrDefault(st => st.Id == DebugAt) is { } at)
        {
            _sim.PlacePlayer(at.Interact);
            var dir = _world.WorkDirection(at);
            _player.FaceInstantly(MathF.Atan2(dir.X, dir.Z) * 180f / MathF.PI);
            _debugUsePending = DebugUse; // pressed on the first step after READY
        }
        foreach (var toy in DebugCartLoad ?? []) // --cart-load train,bear: toys already in the cart
            _sim.State.Cart.Items.Add(new Item { ToyId = toy, Stage = ItemStage.Assembled });
        _pause.BeginReady();
        _lastPosition = _sim.State.PlayerPosition;
        _lastFacing = _player.FacingDegrees;
        _camera.Update(System.Numerics.Vector2.Zero, _sim.State.PlayerPosition, CurrentWork(), dt: 10);
    }

    // Where the elf was at the last simulation step, so frames drawn between steps can place it part of the way along.
    private Point2 _lastPosition;
    private float _lastFacing;

    /// <summary>The elf's position for this frame: between its last two simulation steps (see <see cref="FrameClock"/>).</summary>
    private Point2 DrawnPosition
    {
        get
        {
            var now = _sim.State.PlayerPosition;
            double t = FrameClock.StepFraction;
            return new Point2(_lastPosition.X + (now.X - _lastPosition.X) * t, _lastPosition.Y + (now.Y - _lastPosition.Y) * t);
        }
    }

    /// <summary>The elf's heading for this frame, turned the short way round from its last step's.</summary>
    private float DrawnFacing
    {
        get
        {
            float diff = (_player.FacingDegrees - _lastFacing) % 360f;
            if (diff > 180f) diff -= 360f;
            if (diff < -180f) diff += 360f;
            return _lastFacing + diff * FrameClock.StepFraction;
        }
    }

    private void DebugStartAssembly(int parts)
    {
        var toy = _s.Content.FindToy(DebugAssembleToy ?? "") ?? _s.Content.Toys[0];
        var bench = _s.Content.Stations.First(st => st.Type == StationType.Bench);
        _sim.State.CarriedItem = new Item { ToyId = toy.Id };
        _sim.PlacePlayer(bench.Interact);
        _sim.Step(new PlayerCommand(0, 0, Interact: true));
        // With a variant named, follow that variant's combination; otherwise whatever the bench suggests.
        var follow = DebugAssembleVariant != null ? toy.Variant(DebugAssembleVariant) : null;
        for (int i = 0; i < parts && _sim.State.Assembly is { } game && game.Target is { } target && i < target.Order.Count - 1; i++)
        {
            string next = follow != null && i < follow.Order.Count ? follow.Order[i] : game.NextPart!;
            _sim.Step(new PlayerCommand(0, 0, false, PartKey: game.KeyOf(next)));
        }
        _sim.DrainEvents();
        var dir = _world.WorkDirection(bench);
        _player.FaceInstantly(MathF.Atan2(dir.X, dir.Z) * 180f / MathF.PI);
    }

    /// <summary>
    /// Where the camera leans in over the elf's shoulder: the bench while building, the ribbon station while choosing
    /// and tying a ribbon.
    /// </summary>
    private WorkFocus? CurrentWork()
    {
        var state = _sim.State;
        string? id = state.Assembly?.StationId ?? state.RibbonPick?.StationId
            ?? (state.CraftingAction is { Type: CraftingType.Ribbon } tie ? tie.StationId : null);
        var station = id == null ? null : _s.Content.Stations.FirstOrDefault(st => st.Id == id);
        return station == null ? null : new WorkFocus(_world.WorkSpot(station), _world.WorkDirection(station));
    }

    private Station? RibbonPickStation =>
        _sim.State.RibbonPick is { } pick ? _s.Content.Stations.FirstOrDefault(st => st.Id == pick.StationId) : null;

    /// <summary>The spool under the mouse pointer while choosing a ribbon, or -1.</summary>
    private int SpoolUnderPointer()
    {
        if (RibbonPickStation is not { } station || _s.Input.UsingGamepad) return -1;
        var pointer = UiCanvas.FromScreen(_s.Input.Pointer(), Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        int best = -1;
        float bestDistance = 46; // UI pixels
        for (int i = 0; i < _s.Content.Tuning.RibbonColors.Length; i++)
        {
            if (_world.SpoolOnScreen(station, i) is not { } at) continue;
            float d = System.Numerics.Vector2.Distance(at, pointer);
            if (d < bestDistance) (best, bestDistance) = (i, d);
        }
        return best;
    }

    /// <summary>The loose piece under the mouse pointer while building at the bench, or null.</summary>
    private string? PieceUnderPointer()
    {
        if (_sim.State.Assembly is not { } game || _s.Input.UsingGamepad || _pause.Blocking) return null;
        var bench = _s.Content.Stations.FirstOrDefault(st => st.Id == game.StationId);
        if (bench == null) return null;
        var pointer = UiCanvas.FromScreen(_s.Input.Pointer(), Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        return _world.PieceAt(bench, game, pointer);
    }

    /// <summary>While building, shovelling or tying a ribbon: the floor direction towards that station, so the elf faces it.</summary>
    private (double X, double Y)? FaceWorkDirection()
    {
        var state = _sim.State;
        if (state.Cart.Pushing) return state.Cart.Direction; // standing still, the elf keeps facing the cart
        string? id = state.Assembly?.StationId ?? state.RibbonPick?.StationId ?? state.CraftingAction?.StationId;
        if (id == null || _s.Content.Stations.FirstOrDefault(st => st.Id == id) is not { } station) return null;
        var dir = _world.WorkDirection(station);
        return (dir.X, dir.Z);
    }

    private ElfActivity CurrentActivity() =>
        _sim.State.Assembly != null ? ElfActivity.Hammering
        : _sim.State.CraftingAction is { Type: CraftingType.Shovel } ? ElfActivity.Shovelling
        : ElfActivity.None;

    public void Exit()
    {
        _s.Input.SetMouseCaptured(false);
        _s.World.UseMenuCamera();
    }

    public void FixedUpdate()
    {
        _lastPosition = _sim.State.PlayerPosition;
        _lastFacing = _player.FacingDegrees;

        // Losing window focus pauses the shift (GDD section 11).
        if (_s.PauseOnFocusLoss && !Raylib.IsWindowFocused()) _pause.Pause();

        if (_pause.Update()) return;

        bool cancel = false;
        // Esc is both Pause and Back: with the recycle prompt open it only closes the prompt.
        bool back = _s.Input.ConsumePressed(InputAction.Back), pause = _s.Input.ConsumePressed(InputAction.Pause);
        if ((_sim.State.RecyclePromptOpen || _sim.State.PunchPromptOpen || _sim.State.Cart.PickOpen) && back) cancel = true;
        else if (pause) { _pause.Pause(); return; }

        var floorMove = _camera.ToFloorDirection(_s.Input.Move());
        var (mx, my) = _player.ReadMove(floorMove, working: _sim.State.IsWorking,
            faceWork: FaceWorkDirection(), dt: 1f / _sim.Tuning.TicksPerSecond);
        if (DebugPause) { DebugPause = false; _pause.Pause(); return; }
        bool interact = _interaction.ReadInteract() || _debugUsePending;
        _debugUsePending = false;

        // Part keys only mean something in the bench minigame and when choosing a ribbon (key n = spool n);
        // otherwise they are dropped. Clicking a spool counts as pressing its key.
        int partKey = _s.Input.ConsumePartKey();
        _mouse.Update();
        bool click = _mouse.Clicked;
        // The punch-clock and recycle questions have buttons: clicking one answers it like the keys would.
        bool asking = _sim.State.PunchPromptOpen || _sim.State.RecyclePromptOpen;
        _hud.ConfirmHovered = asking ? _hud.Hits.At(_mouse.At) : -1;
        if (asking && click)
        {
            if (_hud.ConfirmHovered == 0) interact = true;
            else if (_hud.ConfirmHovered == 1) cancel = true;
        }
        if (_sim.State.RibbonPick != null)
        {
            if (partKey == 0 && click && _world.HoveredSpool >= 0) partKey = _world.HoveredSpool + 1;
            if (DebugSpool is { } spool && _sim.State.Tick > 30) { partKey = spool; DebugSpool = null; }
        }
        else if (_sim.State.Assembly is { } build)
        {
            // Clicking a loose piece on the bench counts as pressing its key.
            if (partKey == 0 && click && _world.HoveredPiece is { } piece) partKey = build.KeyOf(piece);
            if (partKey != 0) _swings++;
        }
        else if (_sim.State.Cart.PickOpen)
        {
            // The open cart's cards: clicking toy n takes it (like part key n), the push button grabs the handle.
            _hud.CartHovered = _hud.Hits.At(_mouse.At);
            if (click && _hud.CartHovered >= HudView.CartSlotHit && _hud.CartHovered < HudView.CartPushHit)
                partKey = _hud.CartHovered - HudView.CartSlotHit + 1;
            else if (click && _hud.CartHovered == HudView.CartPushHit) interact = true;
        }
        else { while (_s.Input.ConsumePartKey() != 0) { } partKey = 0; }
        if (!_sim.State.Cart.PickOpen) _hud.CartHovered = -1;

        _sim.Step(new PlayerCommand(mx, my, interact, cancel, partKey));

        var events = _sim.DrainEvents();
        // Taking hold of the cart turns the elf to face it at once, so the handle is in their hands.
        if (events.Any(e => e.Type == SimEventType.CartGrabbed) && _sim.State.Cart.Direction is var (cx, cy))
            _player.FaceInstantly(MathF.Atan2((float)cx, (float)cy) * 180f / MathF.PI);
        _interaction.Handle(events);
        PlayShiftSounds();
        if (_guideWasActive && !TutorialGuide.Active(_sim) && _sim.State.Phase == RunPhase.Playing)
            _interaction.Feedback.ShowToast(_s.Content.Dialogue.Message("guide_done"), ToastKind.Success, 200);
        _guideWasActive = TutorialGuide.Active(_sim);
        _interaction.Tick();

        if (_sim.State.Phase == RunPhase.Results) FinishRound();
    }

    public void FrameUpdate()
    {
        // Mouse look only while actually playing; menus and READY get a normal cursor.
        // Choosing a ribbon or building at the bench frees the pointer too, so the spools and the loose pieces can be
        // clicked (the camera holds still meanwhile).
        bool playing = !_pause.Blocking || _pause.State == PauseState.Ready;
        bool picking = _sim.State.RibbonPick != null || _sim.State.Assembly != null || _sim.State.Cart.PickOpen;
        bool asking = _sim.State.PunchPromptOpen || _sim.State.RecyclePromptOpen; // its buttons can be clicked
        _s.Input.SetMouseCaptured(playing && !picking && !asking && Raylib.IsWindowFocused());
        _camera.Follow = _s.Settings.CameraFollow;
        _camera.Update(_pause.Blocking || picking || asking ? System.Numerics.Vector2.Zero : _s.Input.Look(), DrawnPosition,
            CurrentWork(), Raylib.GetFrameTime(), DrawnFacing);
        _world.PlayerAt = DrawnPosition;
        _world.HoveredSpool = _sim.State.RibbonPick != null ? SpoolUnderPointer() : -1;
        _world.HoveredPiece = PieceUnderPointer();
        _world.GuideTarget = _pause.Blocking ? null : TutorialGuide.Next(_sim)?.Station;

        _frames++;
        if (DebugSwingEvery is { } every && every > 0 && _frames % every == 0) _swings++;
    }

    public void DrawWorld() =>
        _world.Draw(_sim, DrawnFacing, _player.Bob, new ElfPose(_player.Walking, CurrentActivity(), _swings));

    /// <summary>Debug builds only (--cover): draw the cover art instead of the HUD, for the project's cover image.</summary>
    public static bool DebugCover;

    public void DrawUi(UiCanvas ui)
    {
        if (DebugCover) { MenuView.DrawCover(ui); return; }
        _hud.Draw(ui, _sim, _interaction.Feedback, _world, _run.TotalScore, _s.Settings.Controls);
        if (RibbonPickStation is { } station && !_pause.Blocking)
            _world.DrawSpoolLabels(ui, station, i => _s.Input.Label(InputAction.Part1 + i));
        _pause.Draw(ui);
    }

    // Sounds that come from watching the state rather than from one event.
    private const double SpiritLowFraction = 0.25;
    /// <summary>
    /// Seconds between footfalls: the walk animation loops every 0.67 s with a foot landing at its start and halfway
    /// (frames 0 and 10 of 20 at 30 fps, tools/blender/make_models.py), and it plays at that rate whatever the speed.
    /// </summary>
    private const double SecondsPerStep = 20.0 / 30.0 / 2;
    private long _lastTickSecond = -1;
    private bool? _wasJolly;
    private bool? _wasLow;
    private double _walkClock = -1; // seconds since the elf started walking; below 0 when standing
    private int _lastFootstep;
    private readonly Random _footRandom = new();

    private void PlayShiftSounds()
    {
        var st = _sim.State;
        if (st.Phase != RunPhase.Playing) return;
        var audio = _s.Audio;

        // A quiet tick each second of the last 10.
        long ticksLeft = _sim.RemainingShiftTicks;
        long second = (ticksLeft + _sim.Tuning.TicksPerSecond - 1) / _sim.Tuning.TicksPerSecond;
        if (second is > 0 and <= 10 && second != _lastTickSecond) audio.Play(SoundIds.ClockTick);
        _lastTickSecond = second;

        // Spirit: sleigh bells on reaching JOLLY, a gust of wind (once) on dropping below a quarter.
        var spirit = _sim.Tuning.Spirit;
        if (spirit.Enabled)
        {
            bool jolly = st.Spirit >= spirit.JollyAt, low = st.Spirit < spirit.Max * SpiritLowFraction;
            if (_wasJolly == false && jolly) audio.Play(SoundIds.JollyOn);
            if (_wasLow == false && low) audio.Play(SoundIds.SpiritLow);
            (_wasJolly, _wasLow) = (jolly, low);
        }

        // Footsteps on the walk animation's footfalls: the first as the elf sets off (the clip starts with a foot down),
        // then one every half loop. Standing still or working resets it.
        if (!_player.Walking || st.IsWorking) { _walkClock = -1; return; }
        double before = _walkClock;
        _walkClock += 1.0 / _sim.Tuning.TicksPerSecond;
        if (before >= 0 && Math.Floor(_walkClock / SecondsPerStep) == Math.Floor(before / SecondsPerStep)) return;
        if (before < 0) _walkClock = 0;
        int pick = (_lastFootstep + 1 + _footRandom.Next(SoundIds.Footsteps.Length - 1)) % SoundIds.Footsteps.Length; // never the same twice
        _lastFootstep = pick;
        audio.Play(SoundIds.Footsteps[pick], pitch: 0.94f + 0.12f * (float)_footRandom.NextDouble());
    }

    private void FinishRound()
    {
        var st = _sim.State;
        var d = _s.Content.Dialogue;
        int quota = _sim.Quota;
        int round = _run.Round;
        bool passed = _run.CompleteRound(st, quota, _s.Content.Tuning.Cart);

        // Lifetime totals and unlocks, saved straight away.
        var unlocked = _s.Progress.RecordRound(st, round, passed, _s.Content.Unlocks);
        _s.SaveProgress();

        bool newBest = false;
        if (!passed)
        {
            newBest = _s.Best.Submit(_run.Mode, _run.TotalScore, _run.Round);
            _s.Save.SaveBest(_s.Best);
        }

        var cue = d.Cue(_sim.ReviewCueId);
        var summary = new ResultsSummary(
            _run, passed,
            Heading: passed ? d.Message("round_complete", _run.Round) : st.SpiritOut ? d.Message("spirit_out") : d.Message("game_over"),
            ReviewLabel: d.Message(Scoring.ReviewKey(st.Delivered, _sim.Tuning))
                + (st.PunchedOut ? "   /   " + d.Message("punched_out_left", ClockText(st.TicksLeftAtPunchOut)) : ""),
            SantaLine: cue?.Santa ?? "",
            st.Delivered, quota, st.Missed, st.Unfinished, st.Score, st.BestStreak,
            NextQuota: passed ? _sim.Tuning.QuotaFor(_run.Mode, _run.Round + 1) : 0,
            BestScore: _s.Best.For(_run.Mode), BestRound: _s.Best.RoundFor(_run.Mode), NewBest: newBest,
            Unlocked: unlocked.Select(u => u.Name).ToList(), Spirit: st.Spirit,
            NextNote: passed ? NextRoundNote(_run.Round + 1) : "", CartNote: CartNote());

        _s.Scenes.Switch(new ResultsController(_s, summary));
    }

    /// <summary>The toy cart part the round just earned ("New toy cart part: Wheels (1 of 3)."), or "".</summary>
    private string CartNote()
    {
        if (_run.NewCartPart is not { } part) return "";
        var cart = _s.Content.Tuning.Cart;
        return cart.BuiltWith(_run.CartParts)
            ? _s.Content.Dialogue.Message("cart_part_ready", part, cart.Capacity)
            : _s.Content.Dialogue.Message("cart_part_earned", part, _run.CartParts, cart.Parts.Length);
    }

    /// <summary>What changes next round: the quota, and the steady flow of letters or a rush shift when one starts.</summary>
    private string NextRoundNote(int next)
    {
        var t = _sim.Tuning;
        var d = _s.Content.Dialogue;
        int quota = t.QuotaFor(_run.Mode, next);
        // A toy joining the line next round (the boat) is the bigger news.
        if (_s.Content.Toys.FirstOrDefault(toy => toy.FromRound > 1 && toy.FromRound == next) is { } newToy)
            return d.Message("next_round_new_toy", next, quota, newToy.Name.ToLowerInvariant());
        string key = t.Rounds.IsRushShift(next) ? "next_round_rush"
            : t.Flow.On(next) && !t.Flow.On(next - 1) ? "next_round_flow" : "next_round";
        return d.Message(key, next, quota);
    }

    /// <summary>Shift-clock time as m:ss.</summary>
    private string ClockText(long ticks)
    {
        long seconds = (ticks + _sim.Tuning.TicksPerSecond - 1) / _sim.Tuning.TicksPerSecond;
        return $"{seconds / 60}:{seconds % 60:00}";
    }
}
