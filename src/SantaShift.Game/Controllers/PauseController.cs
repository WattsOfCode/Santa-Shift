using SantaShift.Core.Rules;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Controllers;

public enum PauseState { Running, Paused, ConfirmRestart, ConfirmQuit, Ready }

/// <summary>
/// Pause menu (resume, restart, settings, quit), with the player's controls beside it, and the one-second READY
/// countdown (GDD sections 11 and 14). While anything here is showing, the simulation does not step.
/// </summary>
public sealed class PauseController(GameServices s, Simulation sim, Action restart, Action quitToTitle)
{
    private enum Item { Resume, Restart, Settings, Quit }
    private const int ItemCount = 4;

    private const int ReadyTicks = 60;
    private int _readyLeft;
    private readonly MenuView _menu = new();
    private readonly SettingsMenu _settings = new(s);
    private readonly MenuMouse _mouse = new(s.Input);
    private Item _selected;
    private int _confirmHovered = -1;

    public PauseState State { get; private set; } = PauseState.Running;
    public bool Blocking => State != PauseState.Running;

    /// <summary>Starts with a READY countdown.</summary>
    public void BeginReady()
    {
        State = PauseState.Ready;
        _readyLeft = ReadyTicks;
    }

    public void Pause()
    {
        if (State != PauseState.Running) return;
        sim.Pause();
        State = PauseState.Paused;
        _menu.Hits.Clear();
        _selected = Item.Resume;
        s.Input.Clear(); // presses from play (a held move key) must not move the menu
    }

    /// <summary>Handles menu input. Returns true if the simulation should not step this tick.</summary>
    public bool Update()
    {
        var input = s.Input;
        switch (State)
        {
            case PauseState.Ready:
                if (--_readyLeft <= 0) { sim.Resume(); State = PauseState.Running; }
                return true;

            case PauseState.Paused:
                if (_settings.IsOpen) { _settings.Update(); return true; }
                _mouse.Update();
                int hovered = _menu.Hits.At(_mouse.At);
                if (_mouse.Moved && hovered >= 0 && hovered != (int)_selected) Select(hovered);
                if (_mouse.Clicked && hovered >= 0) { _selected = (Item)hovered; Activate(); return true; }
                if (input.ConsumePressed(InputAction.MenuUp)) Select((int)_selected - 1);
                if (input.ConsumePressed(InputAction.MenuDown)) Select((int)_selected + 1);
                if (input.ConsumePressed(InputAction.Back) | input.ConsumePressed(InputAction.Pause)) Resume();
                else if (input.ConsumePressed(InputAction.Restart)) Ask(PauseState.ConfirmRestart);
                else if (input.ConsumePressed(InputAction.Quit)) Ask(PauseState.ConfirmQuit);
                else if (input.ConsumePressed(InputAction.Confirm)) Activate();
                return true;

            case PauseState.ConfirmRestart:
            case PauseState.ConfirmQuit:
                _mouse.Update();
                _confirmHovered = _menu.Hits.At(_mouse.At);
                bool yes = _mouse.Clicked ? _confirmHovered == 0 : input.ConsumePressed(InputAction.Confirm);
                bool no = _mouse.Clicked && _confirmHovered == 1;
                if (yes)
                {
                    if (State == PauseState.ConfirmRestart) restart(); else quitToTitle();
                }
                else if (no || input.ConsumePressed(InputAction.Back)) { State = PauseState.Paused; _menu.Hits.Clear(); }
                return true;

            default:
                return false;
        }
    }

    private void Activate()
    {
        s.Audio.Play(SoundIds.UiConfirm);
        switch (_selected)
        {
            case Item.Resume: Resume(); break;
            case Item.Restart: Ask(PauseState.ConfirmRestart); break;
            case Item.Settings: _settings.Open(canResetProgress: false); break;
            case Item.Quit: Ask(PauseState.ConfirmQuit); break;
        }
    }

    /// <summary>Opens a yes / no question; the old buttons are forgotten until it is drawn.</summary>
    private void Ask(PauseState question)
    {
        State = question;
        _menu.Hits.Clear();
        _confirmHovered = -1;
    }

    private void Select(int index)
    {
        _selected = (Item)((index + ItemCount) % ItemCount);
        s.Audio.Play(SoundIds.UiMove);
    }

    private void Resume()
    {
        s.Input.Clear();
        BeginReady(); // resume needs an explicit action, then a 1 second READY
    }

    /// <summary>During READY: the warning when this round's bench works differently from the last.</summary>
    private string? ReadyWarning() => sim.Tuning.Assembly.PolicyChange(sim.State.Mode, sim.State.Round) switch
    {
        "assembly_keys" => s.Content.Dialogue.Message("assembly_warn_keys", sim.State.Round),
        "assembly_order" => s.Content.Dialogue.Message("assembly_warn_order", sim.State.Round),
        _ => null,
    };

    public void Draw(UiCanvas ui)
    {
        switch (State)
        {
            case PauseState.Paused:
                _menu.DrawPause(ui, (int)_selected, s.Settings.Controls, s.Input);
                _settings.Draw(ui);
                break;
            case PauseState.ConfirmRestart: _menu.DrawConfirm(ui, "Restart this shift?", s.Input, _confirmHovered); break;
            case PauseState.ConfirmQuit: _menu.DrawConfirm(ui, "Quit to title? This shift will be lost.", s.Input, _confirmHovered); break;
            case PauseState.Ready: _menu.DrawReady(ui, _readyLeft / 60f, ReadyWarning()); break;
        }
    }
}
