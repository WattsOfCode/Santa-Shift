using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>
/// The start of every run: Santa's naughty and nice list unrolls over the workshop. The children marked naughty
/// will want coal this run. Confirm starts the first shift; Back returns to the title; the Tutorial button (Tab / Y)
/// turns round 1's tutorial helpers on or off (the same switch as Settings > Game > Tutorial helpers, and saved).
/// </summary>
public sealed class NaughtyNiceController(GameServices s, RunProgress run) : IScreen
{
    private const float UnrollSeconds = 0.9f;

    private readonly WorldView _world = new(s.Content, s.Assets, s.World);
    private readonly NaughtyNiceListView _view = new(s.Content);
    private readonly MenuMouse _mouse = new(s.Input);
    private float _time;

    public void FixedUpdate()
    {
        _mouse.Update();
        int hit = _view.Hits.At(_mouse.At);
        _view.Hovered = hit;
        bool toggle = _mouse.Clicked ? hit == 2 : s.Input.ConsumePressed(InputAction.ToggleMode);
        if (toggle && _time >= UnrollSeconds)
        {
            s.Settings.TutorialHelpers = !s.Settings.TutorialHelpers;
            s.SaveSettings();
            s.Audio.Play(SoundIds.UiConfirm);
            return;
        }
        if (_mouse.Clicked && _time < UnrollSeconds) { _time = UnrollSeconds; return; } // a click just finishes unrolling
        bool start = _mouse.Clicked ? hit == 0 : s.Input.ConsumePressed(InputAction.Confirm);
        bool back = _mouse.Clicked && hit == 1;
        if (start)
        {
            if (_time < UnrollSeconds) { _time = UnrollSeconds; return; } // first press just finishes unrolling
            s.Audio.Play(SoundIds.UiConfirm);
            // With the Head Start unlock, the run begins with a perk pick.
            var pick = s.Progress.HasHeadStart(s.Content.Unlocks) ? new PerkPickController(s, run, beforeFirstRound: true) : null;
            s.Scenes.Switch(pick is { Offers.Count: > 0 } ? pick : new WorkshopController(s, run));
        }
        else if (back || s.Input.ConsumePressed(InputAction.Back))
        {
            s.Scenes.Switch(new TitleController(s));
        }
    }

    public void FrameUpdate() => _time += s.Settings.ReducedMotion ? UnrollSeconds : Raylib.GetFrameTime();

    public void DrawWorld() => _world.DrawRoom();

    public void DrawUi(UiCanvas ui)
    {
        _view.TutorialOn = s.Settings.TutorialHelpers;
        _view.Draw(ui, run.List, _time / UnrollSeconds, s.Input);
    }
}
