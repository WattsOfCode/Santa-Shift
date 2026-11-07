using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>
/// The start of every run: Santa's naughty and nice list unrolls over the workshop. The children marked naughty
/// will want coal this run. Confirm starts the first shift; Back returns to the title.
/// </summary>
public sealed class NaughtyNiceController(GameServices s, RunProgress run) : IScreen
{
    private const float UnrollSeconds = 0.9f;

    private readonly WorldView _world = new(s.Content, s.Assets, s.World);
    private readonly NaughtyNiceListView _view = new(s.Content);
    private float _time;

    public void FixedUpdate()
    {
        if (s.Input.ConsumePressed(InputAction.Confirm))
        {
            if (_time < UnrollSeconds) { _time = UnrollSeconds; return; } // first press just finishes unrolling
            s.Audio.Play(SoundIds.UiConfirm);
            // With the Head Start unlock, the run begins with a perk pick.
            var pick = s.Progress.HasHeadStart(s.Content.Unlocks) ? new PerkPickController(s, run, beforeFirstRound: true) : null;
            s.Scenes.Switch(pick is { Offers.Count: > 0 } ? pick : new WorkshopController(s, run));
        }
        else if (s.Input.ConsumePressed(InputAction.Back))
        {
            s.Scenes.Switch(new TitleController(s));
        }
    }

    public void FrameUpdate() => _time += s.Settings.ReducedMotion ? UnrollSeconds : Raylib.GetFrameTime();

    public void DrawWorld() => _world.DrawRoom();

    public void DrawUi(UiCanvas ui) => _view.Draw(ui, run.List, _time / UnrollSeconds, s.Input);
}
