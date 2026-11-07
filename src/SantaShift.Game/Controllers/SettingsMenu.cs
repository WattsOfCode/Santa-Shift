using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Controllers;

public enum SettingsTab { Sound, Controls, Game }

/// <summary>
/// The settings screen, opened from the title or the pause menu: Sound (volumes, mute), Controls (two keys and one
/// controller button per action, rebound by pressing them) and Game (reduced motion, look speed).
/// Works the same with keyboard or controller. Changes apply at once and are saved when it closes.
/// </summary>
public sealed class SettingsMenu(GameServices s)
{
    private const double CaptureGraceSeconds = 0.2; // the press that started rebinding is not the new key
    private const double CaptureTimeoutSeconds = 6;

    private readonly SettingsView _view = new();

    public bool IsOpen { get; private set; }
    public SettingsTab Tab { get; private set; }

    /// <summary>The selected row: 0 is the tab bar, then the tab's rows.</summary>
    public int Row { get; private set; }

    /// <summary>On the Controls tab: 0 and 1 are the two keyboard keys, 2 the controller button.</summary>
    public int Column { get; private set; }

    /// <summary>True while waiting for the player to press the new key or button.</summary>
    public bool Capturing { get; private set; }
    private double _captureStarted;

    public Settings Settings => s.Settings;
    public IInputService Input => s.Input;

    public void Open(SettingsTab tab = SettingsTab.Sound)
    {
        IsOpen = true;
        Tab = tab;
        Row = 1;
        Column = 0;
        Capturing = false;
        s.Input.Clear();
    }

    /// <summary>Rows on the current tab, after the tab bar.</summary>
    public int RowCount => Tab switch
    {
        SettingsTab.Sound => 4,
        SettingsTab.Controls => ControlBindings.Actions.Length + 1, // + reset
        _ => 3,
    };

    public void Update()
    {
        if (!IsOpen) return;
        var input = s.Input;
        if (Capturing) { UpdateCapture(); return; }

        if (input.ConsumePressed(InputAction.Back)) { Close(); return; }
        if (input.ConsumePressed(InputAction.TabNext)) SwitchTab(+1);
        if (input.ConsumePressed(InputAction.TabPrev)) SwitchTab(-1);
        if (input.ConsumePressed(InputAction.MenuUp)) { Row = (Row + RowCount) % (RowCount + 1); Beep(); }
        if (input.ConsumePressed(InputAction.MenuDown)) { Row = (Row + 1) % (RowCount + 1); Beep(); }

        int step = (input.ConsumePressed(InputAction.MenuRight) ? 1 : 0) - (input.ConsumePressed(InputAction.MenuLeft) ? 1 : 0);
        bool confirm = input.ConsumePressed(InputAction.Confirm);
        bool clear = input.ConsumePressed(InputAction.ClearBinding);

        if (Row == 0) { if (step != 0) SwitchTab(step); return; }
        switch (Tab)
        {
            case SettingsTab.Sound: UpdateSound(step, confirm); break;
            case SettingsTab.Controls: UpdateControls(step, confirm, clear); break;
            case SettingsTab.Game: UpdateGame(step, confirm); break;
        }
    }

    public void Draw(UiCanvas ui)
    {
        if (IsOpen) _view.Draw(ui, this);
    }

    private void UpdateSound(int step, bool confirm)
    {
        var st = s.Settings;
        switch (Row)
        {
            case 1: st.MasterVolume = Nudge(st.MasterVolume, step * 0.05f, 0, 1); break;
            case 2: st.MusicVolume = Nudge(st.MusicVolume, step * 0.05f, 0, 1); break;
            case 3: st.EffectsVolume = Nudge(st.EffectsVolume, step * 0.05f, 0, 1); break;
            case 4: if (step != 0 || confirm) st.Muted = !st.Muted; break;
        }
        if (step != 0 || confirm) { s.Audio.ApplySettings(st); s.Audio.Play(SoundIds.UiConfirm); }
    }

    private void UpdateGame(int step, bool confirm)
    {
        var st = s.Settings;
        switch (Row)
        {
            case 1: if (step != 0 || confirm) st.ReducedMotion = !st.ReducedMotion; break;
            case 2: st.MouseSensitivity = Nudge(st.MouseSensitivity, step * 0.25f, 0.25f, 3); break;
            case 3: st.StickSensitivity = Nudge(st.StickSensitivity, step * 0.25f, 0.25f, 3); break;
        }
        if (step != 0 || confirm) Beep();
    }

    private void UpdateControls(int step, bool confirm, bool clear)
    {
        if (step != 0) { Column = Math.Clamp(Column + step, 0, 2); Beep(); }
        bool reset = Row == RowCount;
        if (reset)
        {
            if (confirm) { s.Settings.Controls = ControlBindings.Defaults(); s.Input.Apply(s.Settings.Controls); Beep(); }
            return;
        }
        string action = ControlBindings.Actions[Row - 1];
        if (clear)
        {
            if (Column < ControlBindings.KeySlots) s.Settings.Controls.SetKey(action, Column, "");
            else s.Settings.Controls.SetPad(action, "");
            s.Input.Apply(s.Settings.Controls);
            Beep();
        }
        else if (confirm)
        {
            Capturing = true;
            _captureStarted = Raylib.GetTime();
        }
    }

    private void UpdateCapture()
    {
        var input = s.Input;
        double waited = Raylib.GetTime() - _captureStarted;
        bool cancel = input.ConsumePressed(InputAction.Back) && Column < ControlBindings.KeySlots
                      || input.ConsumePressed(InputAction.Pause) && Column == ControlBindings.KeySlots
                      || waited > CaptureTimeoutSeconds;
        string action = ControlBindings.Actions[Row - 1];
        if (cancel) { EndCapture(); return; }
        if (waited < CaptureGraceSeconds) { input.TryCaptureKey(out _); input.TryCaptureButton(out _); return; }

        if (Column < ControlBindings.KeySlots && input.TryCaptureKey(out string key))
        {
            s.Settings.Controls.SetKey(action, Column, key);
            EndCapture();
        }
        else if (Column == ControlBindings.KeySlots && input.TryCaptureButton(out string button))
        {
            s.Settings.Controls.SetPad(action, button);
            EndCapture();
        }
    }

    private void EndCapture()
    {
        Capturing = false;
        s.Input.Apply(s.Settings.Controls);
        s.Input.Clear(); // the new key must not also act in the menu
        Beep();
    }

    private void SwitchTab(int step)
    {
        Tab = (SettingsTab)(((int)Tab + step + 3) % 3);
        Row = Math.Min(Row, RowCount);
        Column = 0;
        Beep();
    }

    private void Close()
    {
        IsOpen = false;
        s.Settings.Clamp();
        s.Input.Apply(s.Settings.Controls);
        s.Audio.ApplySettings(s.Settings);
        s.SaveSettings();
        s.Input.Clear();
    }

    private void Beep() => s.Audio.Play(SoundIds.UiConfirm);

    private static float Nudge(float v, float by, float min, float max) => MathF.Round(Math.Clamp(v + by, min, max) * 100) / 100;
}
