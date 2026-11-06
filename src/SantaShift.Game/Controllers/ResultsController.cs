using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>Performance review: play again or return to the title.</summary>
public sealed class ResultsController(GameServices s, ResultsSummary summary) : IScreen
{
    private readonly WorldView _world = new(s.Content, s.Assets, s.World);
    private readonly ResultsView _view = new();

    public ResultsSummary Summary => summary;

    public void FixedUpdate()
    {
        if (s.Input.ConsumePressed(InputAction.Confirm))
        {
            s.Audio.Play(SoundIds.UiConfirm);
            s.Scenes.Switch(new WorkshopController(s, summary.Mode));
        }
        else if (s.Input.ConsumePressed(InputAction.Back))
        {
            s.Scenes.Switch(new TitleController(s));
        }
    }

    public void DrawWorld() => _world.DrawRoom();

    public void DrawUi(UiCanvas ui) => _view.Draw(ui, summary);
}
