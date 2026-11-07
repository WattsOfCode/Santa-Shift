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

    public ResultsSummary Summary => summary;

    public void FixedUpdate()
    {
        var run = summary.Run;
        if (s.Input.ConsumePressed(InputAction.Confirm))
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
        else if (s.Input.ConsumePressed(InputAction.Back))
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

    public void DrawUi(UiCanvas ui) => _view.Draw(ui, summary, s.Input);

    private void RecordEndedRun(RunProgress run)
    {
        s.Best.Submit(run.Mode, run.TotalScore, run.Round);
        s.Save.SaveBest(s.Best);
    }
}
