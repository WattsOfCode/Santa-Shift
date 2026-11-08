using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>Title screen: a short menu (start, mode, settings, quit), driven by keyboard or controller.</summary>
public sealed class TitleController(GameServices s) : IScreen
{
    public enum Item { Start, Mode, Unlocks, Settings, Quit }
    private static readonly int ItemCount = Enum.GetValues<Item>().Length;

    private readonly WorldView _world = new(s.Content, s.Assets, s.World);
    private readonly MenuView _menu = new();
    private readonly SettingsMenu _settings = new(s);
    private readonly UnlocksMenu _unlocks = new(s);
    private Item _selected = Item.Start;

    /// <summary>Debug builds only (--settings N): open the settings on tab N, for screenshots.</summary>
    public static int? DebugSettingsTab;

    /// <summary>Debug builds only (--unlocks): open the unlocks list, for screenshots.</summary>
    public static bool DebugUnlocks;

    public void Enter()
    {
        if (DebugSettingsTab is { } tab) _settings.Open((SettingsTab)tab);
        if (DebugUnlocks) _unlocks.Open();
    }

    public void FixedUpdate()
    {
        if (_settings.IsOpen) { _settings.Update(); return; }
        if (_unlocks.IsOpen) { _unlocks.Update(); return; }
        var input = s.Input;

        if (input.ConsumePressed(InputAction.MenuUp)) Select((int)_selected - 1);
        if (input.ConsumePressed(InputAction.MenuDown)) Select((int)_selected + 1);
        bool sideways = input.ConsumePressed(InputAction.MenuLeft) | input.ConsumePressed(InputAction.MenuRight);

        if (input.ConsumePressed(InputAction.ToggleMode) || (sideways && _selected == Item.Mode)) ToggleMode();
        else if (input.ConsumePressed(InputAction.Confirm)) Activate();
        else if (input.ConsumePressed(InputAction.Back)) s.Scenes.RequestQuit();
    }

    private void Select(int index)
    {
        _selected = (Item)((index + ItemCount) % ItemCount);
        s.Audio.Play(SoundIds.UiMove);
    }

    private void Activate()
    {
        s.Audio.Play(SoundIds.UiConfirm);
        switch (_selected)
        {
            case Item.Start: s.Scenes.Switch(WorkshopController.NewRun(s, s.Settings.PreferredMode)); break;
            case Item.Mode: ToggleMode(); break;
            case Item.Unlocks: _unlocks.Open(); break;
            case Item.Settings: _settings.Open(); break;
            case Item.Quit: s.Scenes.RequestQuit(); break;
        }
    }

    private void ToggleMode()
    {
        s.Settings.PreferredMode = s.Settings.PreferredMode == GameMode.Standard ? GameMode.Relaxed : GameMode.Standard;
        s.SaveSettings();
        s.Audio.Play(SoundIds.UiConfirm);
    }

    public void DrawWorld() => _world.DrawRoom();

    public void DrawUi(UiCanvas ui)
    {
        var mode = s.Settings.PreferredMode;
        _menu.DrawTitle(ui, mode, s.Best.For(mode), s.Best.RoundFor(mode), Raylib_cs.Raylib.GetTime(), s.Settings.ReducedMotion,
            (int)_selected, s.Input);
        _settings.Draw(ui);
        _unlocks.Draw(ui);
    }
}
