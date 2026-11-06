using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>The gameplay screen: runs one shift and coordinates its views.</summary>
public sealed class WorkshopController : IScreen
{
    private readonly GameServices _s;
    private readonly GameMode _mode;
    private readonly Simulation _sim;
    private readonly PlayerController _player;
    private readonly InteractionController _interaction;
    private readonly PauseController _pause;
    private readonly WorldView _world;
    private readonly HudView _hud;

    public WorkshopController(GameServices services, GameMode mode)
    {
        _s = services;
        _mode = mode;
        _sim = new Simulation(services.Content, mode);
        _player = new PlayerController(services.Input, services.Settings);
        _interaction = new InteractionController(services.Content, services.Input, services.Audio);
        _interaction.Attach(_sim);
        _pause = new PauseController(services.Input, _sim,
            restart: () => services.Scenes.Switch(new WorkshopController(services, mode)),
            quitToTitle: () => services.Scenes.Switch(new TitleController(services)));
        _world = new WorldView(services.Content, services.Assets, services.World);
        _hud = new HudView(services.Content);
    }

    public Simulation Simulation => _sim;

    public void Enter() => _pause.BeginReady();

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

        var (mx, my) = _player.ReadMove(working: _sim.State.CraftingAction != null);
        bool interact = _interaction.ReadInteract();
        _sim.Step(new PlayerCommand(mx, my, interact, cancel));

        _interaction.Handle(_sim.DrainEvents());
        _interaction.Tick();

        if (_sim.State.Phase == RunPhase.Results) FinishShift();
    }

    public void DrawWorld() => _world.Draw(_sim, _player.FacingDegrees, _player.Bob);

    public void DrawUi(UiCanvas ui)
    {
        _hud.Draw(ui, _sim, _interaction.Feedback, _world);
        _pause.Draw(ui);
    }

    private void FinishShift()
    {
        var st = _sim.State;
        var t = _sim.Tuning;
        var d = _s.Content.Dialogue;
        bool passed = st.Delivered >= _sim.Quota;
        bool newBest = _s.Best.Submit(_mode, st.Score);
        if (newBest) _s.Save.SaveBest(_s.Best);

        var cue = d.Cue(passed ? "review_pass" : "review_miss");
        var summary = new ResultsSummary(
            _mode, passed,
            Heading: cue?.Text ?? (passed ? "Shift complete." : "Quota missed."),
            ReviewLabel: d.Message(Scoring.ReviewKey(st.Delivered, t)),
            SantaLine: cue?.Santa ?? "",
            st.Delivered, _sim.Quota, st.Missed, st.Unfinished, st.Score, st.BestStreak,
            _s.Best.For(_mode), newBest);

        _s.Scenes.Switch(new ResultsController(_s, summary));
    }
}
