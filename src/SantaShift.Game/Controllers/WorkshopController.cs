using Raylib_cs;
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
        _sim = new Simulation(services.Content, run.Mode, run.Round, seed: Random.Shared.Next());
        _player = new PlayerController(services.Input, services.Settings);
        _interaction = new InteractionController(services.Content, services.Input, services.Audio);
        _interaction.Attach(_sim);
        _pause = new PauseController(services.Input, _sim,
            restart: () => services.Scenes.Switch(new WorkshopController(services, new RunProgress(run.Mode))),
            quitToTitle: () => services.Scenes.Switch(new TitleController(services)));
        _camera = new CameraController(services.Render.ChaseCamera, services.Render.ShoulderCamera, services.World, services.Content);
        _world = new WorldView(services.Content, services.Assets, services.World);
        _hud = new HudView(services.Content);
    }

    /// <summary>Starts a brand-new run at round 1.</summary>
    public static WorkshopController NewRun(GameServices services, GameMode mode) => new(services, new RunProgress(mode));

    public Simulation Simulation => _sim;

    /// <summary>Debug builds only (--carry toy[:assembled][:ribbon]): start holding an item, for screenshots.</summary>
    public static Item? DebugCarry;

    /// <summary>Debug builds only (--assemble N): start at the bench building a train with N parts on.</summary>
    public static int? DebugAssembleParts;

    /// <summary>Debug builds only (--swing-every N): swing the hammer every N frames, for screenshots of the swing.</summary>
    public static int? DebugSwingEvery;

    /// <summary>Counts hammer swings (one per number key pressed while building); drives the swing animation.</summary>
    private int _swings;
    private int _frames;

    public void Enter()
    {
        if (DebugCarry != null) _sim.State.CarriedItem = DebugCarry;
        if (DebugAssembleParts is { } parts) DebugStartAssembly(parts);
        _pause.BeginReady();
        _camera.Update(System.Numerics.Vector2.Zero, _sim.State.PlayerPosition, CurrentWork(), dt: 10);
    }

    private void DebugStartAssembly(int parts)
    {
        var toy = _s.Content.Toys[0];
        var bench = _s.Content.Stations.First(st => st.Type == StationType.Bench);
        _sim.State.CarriedItem = new Item { ToyId = toy.Id };
        _sim.PlacePlayer(bench.Interact);
        _sim.Step(new PlayerCommand(0, 0, Interact: true));
        for (int i = 0; i < parts && _sim.State.Assembly is { } game && i < game.Order.Count - 1; i++)
            _sim.Step(new PlayerCommand(0, 0, false, PartKey: game.NextKey));
        _sim.DrainEvents();
        var dir = _world.WorkDirection(bench);
        _player.ReadMove((0, 0), working: true, faceWork: (dir.X, dir.Z));
    }

    /// <summary>The bench spot while the elf is building there, for the camera and the elf's facing.</summary>
    private WorkFocus? CurrentWork()
    {
        if (_sim.State.Assembly is not { } game) return null;
        var bench = _s.Content.Stations.FirstOrDefault(st => st.Id == game.StationId);
        return bench == null ? null : new WorkFocus(_world.WorkSpot(bench), _world.WorkDirection(bench));
    }

    public void Exit()
    {
        _s.Input.SetMouseCaptured(false);
        _s.World.UseMenuCamera();
    }

    public void FixedUpdate()
    {
        // Losing window focus pauses the shift (GDD section 11).
        if (_s.PauseOnFocusLoss && !Raylib.IsWindowFocused()) _pause.Pause();

        if (_pause.Update()) return;

        bool cancel = false;
        if (_s.Input.ConsumePressed(InputAction.Back))
        {
            if (_sim.State.RecyclePromptOpen) cancel = true;
            else { _pause.Pause(); return; }
        }

        var floorMove = _camera.ToFloorDirection(_s.Input.Move());
        var work = CurrentWork();
        var (mx, my) = _player.ReadMove(floorMove, working: _sim.State.IsWorking,
            faceWork: work is { } w ? (w.Direction.X, w.Direction.Z) : null);
        bool interact = _interaction.ReadInteract();

        // Number keys only mean something during the bench minigame; otherwise they are dropped.
        int partKey = _s.Input.ConsumePartKey();
        if (_sim.State.Assembly == null) { while (_s.Input.ConsumePartKey() != 0) { } partKey = 0; }
        else if (partKey != 0) _swings++;

        _sim.Step(new PlayerCommand(mx, my, interact, cancel, partKey));

        _interaction.Handle(_sim.DrainEvents());
        _interaction.Tick();

        if (_sim.State.Phase == RunPhase.Results) FinishRound();
    }

    public void FrameUpdate()
    {
        // Mouse look only while actually playing; menus and READY get a normal cursor.
        bool playing = !_pause.Blocking || _pause.State == PauseState.Ready;
        _s.Input.SetMouseCaptured(playing && Raylib.IsWindowFocused());
        _camera.Update(_pause.Blocking ? System.Numerics.Vector2.Zero : _s.Input.MouseDelta(), _sim.State.PlayerPosition,
            CurrentWork(), Raylib.GetFrameTime());

        _frames++;
        if (DebugSwingEvery is { } every && every > 0 && _frames % every == 0) _swings++;
    }

    public void DrawWorld() =>
        _world.Draw(_sim, _player.FacingDegrees, _player.Bob, new ElfPose(_player.Walking, _sim.State.Assembly != null, _swings));

    public void DrawUi(UiCanvas ui)
    {
        _hud.Draw(ui, _sim, _interaction.Feedback, _world, _run.TotalScore);
        _pause.Draw(ui);
    }

    private void FinishRound()
    {
        var st = _sim.State;
        var d = _s.Content.Dialogue;
        int quota = _sim.Quota;
        bool passed = _run.CompleteRound(st, quota);

        bool newBest = false;
        if (!passed)
        {
            newBest = _s.Best.Submit(_run.Mode, _run.TotalScore, _run.Round);
            _s.Save.SaveBest(_s.Best);
        }

        var cue = d.Cue(passed ? "review_pass" : "review_miss");
        var summary = new ResultsSummary(
            _run, passed,
            Heading: passed ? d.Message("round_complete", _run.Round) : d.Message("game_over"),
            ReviewLabel: d.Message(Scoring.ReviewKey(st.Delivered, _sim.Tuning)),
            SantaLine: cue?.Santa ?? "",
            st.Delivered, quota, st.Missed, st.Unfinished, st.Score, st.BestStreak,
            NextQuota: passed ? _sim.Tuning.QuotaFor(_run.Mode, _run.Round + 1) : 0,
            BestScore: _s.Best.For(_run.Mode), BestRound: _s.Best.RoundFor(_run.Mode), NewBest: newBest);

        _s.Scenes.Switch(new ResultsController(_s, summary));
    }
}
