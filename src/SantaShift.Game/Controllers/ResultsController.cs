using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>
/// After a round. Passed: next round (harder) or end the run. Failed: game over, new run or title.
/// </summary>
public sealed class ResultsController(GameServices s, ResultsSummary summary) : IScreen
{
    private readonly WorldView _world = new(s.Content, s.Assets, s.World);
    private readonly ResultsView _view = new();
    private readonly MenuMouse _mouse = new(s.Input);
    private int _hovered = -1;

    public ResultsSummary Summary => summary;

    // The whistle at the end of the shift plays first; the result's jingle follows it, then the unlock sparkle.
    private const int JingleAtTick = 50, UnlockAtTick = 120;
    private int _ticks;

    public void FixedUpdate()
    {
        _ticks++;
        if (_ticks == JingleAtTick) s.Audio.Play(summary.Passed ? SoundIds.RoundClear : SoundIds.RunOver);
        if (_ticks == UnlockAtTick && summary.Unlocked.Count > 0) s.Audio.Play(SoundIds.Unlock);

        var run = summary.Run;
        _mouse.Update();
        _hovered = _view.Hits.At(_mouse.At);
        bool next = _mouse.Clicked ? _hovered == 0 : s.Input.ConsumePressed(InputAction.Confirm);
        bool back = _mouse.Clicked && _hovered == 1;
        if (next)
        {
            s.Audio.Play(SoundIds.UiConfirm);
            if (summary.Passed)
            {
                s.Scenes.Switch(PerkPickController.After(s, run)); // pick a perk, then the next round
            }
            else
            {
                s.Scenes.Switch(WorkshopController.NewRun(s, run.Mode));
            }
        }
        else if (back || s.Input.ConsumePressed(InputAction.Back))
        {
            if (summary.Passed) // leaving a passed run still counts it
            {
                RecordEndedRun(run);
                s.Progress.RecordRunLeft(s.Content.Unlocks);
                s.SaveProgress();
            }
            s.Scenes.Switch(new TitleController(s));
        }
    }

    public void DrawWorld() => _world.DrawRoom();

    public void DrawUi(UiCanvas ui) => _view.Draw(ui, summary, s.Input, _hovered);

    private void RecordEndedRun(RunProgress run)
    {
        s.Best.Submit(run.Mode, run.TotalScore, run.Round);
        s.Save.SaveBest(s.Best);
    }
}
