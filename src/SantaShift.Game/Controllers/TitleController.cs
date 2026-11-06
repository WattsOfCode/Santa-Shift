using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>Title screen: start a shift, pick the mode, or quit.</summary>
public sealed class TitleController(GameServices s) : IScreen
{
    private readonly WorldView _world = new(s.Content, s.Assets, s.World);
    private readonly MenuView _menu = new();

    public void FixedUpdate()
    {
        if (s.Input.ConsumePressed(InputAction.Confirm))
        {
            s.Audio.Play(SoundIds.UiConfirm);
            s.Scenes.Switch(new WorkshopController(s, s.Settings.PreferredMode));
        }
        else if (s.Input.ConsumePressed(InputAction.ToggleMode))
        {
            s.Settings.PreferredMode = s.Settings.PreferredMode == GameMode.Standard ? GameMode.Relaxed : GameMode.Standard;
            s.SaveSettings();
            s.Audio.Play(SoundIds.UiConfirm);
        }
        else if (s.Input.ConsumePressed(InputAction.Back))
        {
            s.Scenes.RequestQuit();
        }
    }

    public void DrawWorld() => _world.DrawRoom();

    public void DrawUi(UiCanvas ui) => _menu.DrawTitle(ui, s.Settings.PreferredMode, s.Best.For(s.Settings.PreferredMode));
}
