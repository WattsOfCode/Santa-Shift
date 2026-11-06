using SantaShift.Core.Rules;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Controllers;

public enum PauseState { Running, Paused, ConfirmRestart, ConfirmQuit, Ready }

/// <summary>
/// Pause menu and the one-second READY countdown (GDD sections 11 and 14).
/// While anything here is showing, the simulation does not step.
/// </summary>
public sealed class PauseController(IInputService input, Simulation sim, Action restart, Action quitToTitle)
{
    private const int ReadyTicks = 60;
    private int _readyLeft;
    private readonly MenuView _menu = new();

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
    }

    /// <summary>Handles menu input. Returns true if the simulation should not step this tick.</summary>
    public bool Update()
    {
        switch (State)
        {
            case PauseState.Ready:
                if (--_readyLeft <= 0) { sim.Resume(); State = PauseState.Running; }
                return true;

            case PauseState.Paused:
                if (input.ConsumePressed(InputAction.Confirm) || input.ConsumePressed(InputAction.Back))
                {
                    input.Clear();
                    BeginReady(); // resume needs an explicit action, then a 1 second READY
                }
                else if (input.ConsumePressed(InputAction.Restart)) State = PauseState.ConfirmRestart;
                else if (input.ConsumePressed(InputAction.Quit)) State = PauseState.ConfirmQuit;
                return true;

            case PauseState.ConfirmRestart:
            case PauseState.ConfirmQuit:
                if (input.ConsumePressed(InputAction.Confirm))
                {
                    if (State == PauseState.ConfirmRestart) restart(); else quitToTitle();
                }
                else if (input.ConsumePressed(InputAction.Back)) State = PauseState.Paused;
                return true;

            default:
                return false;
        }
    }

    public void Draw(UiCanvas ui)
    {
        switch (State)
        {
            case PauseState.Paused: _menu.DrawPause(ui); break;
            case PauseState.ConfirmRestart: _menu.DrawConfirm(ui, "Restart this shift?"); break;
            case PauseState.ConfirmQuit: _menu.DrawConfirm(ui, "Quit to title? This shift will be lost."); break;
            case PauseState.Ready: _menu.DrawReady(ui, _readyLeft / 60f); break;
        }
    }
}
