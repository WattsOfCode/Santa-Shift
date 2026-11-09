using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Controllers;

public enum SettingsTab { Sound, Controls, Game, Credits }

/// <summary>
/// The settings screen, opened from the title or the pause menu: Sound (volumes, mute), Controls (two keys and one
/// controller button per action, rebound by pressing them) and Game (reduced motion, look speed, camera, picture,
/// tutorial guide, frame rate), plus Credits (who made it and what it is built with, from credits.json).
/// Works the same with keyboard or controller. Changes apply at once and are saved when it closes.
/// </summary>
public sealed class SettingsMenu(GameServices s)
{
    private const double CaptureGraceSeconds = 0.2; // the press that started rebinding is not the new key
    private const double CaptureTimeoutSeconds = 6;

    private readonly SettingsView _view = new();
    private readonly MenuMouse _mouse = new(s.Input);
    private int _dragRow; // the slider row being dragged with the mouse, or 0

    /// <summary>True while the mouse is on the close button (drawn gold).</summary>
    public bool CloseHovered { get; private set; }

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
        Row = Math.Min(1, RowCount);
        Column = 0;
        Capturing = false;
        _dragRow = 0;
        _view.Hits.Clear();
        s.Input.Clear();
    }

    /// <summary>Rows on the current tab, after the tab bar.</summary>
    public int RowCount => Tab switch
    {
        SettingsTab.Sound => 4,
        SettingsTab.Controls => ControlBindings.Actions.Length + 1, // + reset
        SettingsTab.Credits => 0, // nothing to change: only the tab bar
        _ => 8,
    };

    public Core.Content.GameContent Content => s.Content;

    public void Update()
    {
        if (!IsOpen) return;
        var input = s.Input;
        _mouse.Update();
        if (Capturing) { UpdateCapture(); return; }

        if (input.ConsumePressed(InputAction.Back)) { Close(); return; }
        if (UpdateMouse()) return;
        if (input.ConsumePressed(InputAction.TabNext)) SwitchTab(+1);
        if (input.ConsumePressed(InputAction.TabPrev)) SwitchTab(-1);
        if (input.ConsumePressed(InputAction.MenuUp)) { Row = (Row + RowCount) % (RowCount + 1); Tick(); }
        if (input.ConsumePressed(InputAction.MenuDown)) { Row = (Row + 1) % (RowCount + 1); Tick(); }

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

    /// <summary>
    /// The mouse: moving over a row selects it, clicking does what Enter would there (a tab opens, a toggle flips, a
    /// choice steps on, a key slot waits for the new key), and the sliders follow a click or drag along their bar.
    /// Returns true when a click was handled (the keys are not read this step).
    /// </summary>
    private bool UpdateMouse()
    {
        int hit = _view.Hits.At(_mouse.At);
        CloseHovered = hit == SettingsView.CloseId;

        if (_dragRow > 0)
        {
            if (MenuMouse.Held && _dragRow == Row) SetSliderFromPointer(_dragRow);
            else _dragRow = 0;
        }

        if (!_mouse.Clicked)
        {
            if (_mouse.Moved) Hover(hit);
            return false;
        }
        if (hit == SettingsView.CloseId) { Close(); return true; }
        for (int t = 0; t < Enum.GetValues<SettingsTab>().Length; t++)
            if (hit == SettingsView.TabId(t))
            {
                if (Tab != (SettingsTab)t) SwitchTab(t - (int)Tab);
                Row = 0;
                return true;
            }
        for (int r = 1; r <= RowCount; r++)
        {
            if (hit == SettingsView.SliderId(r)) { Row = r; _dragRow = r; SetSliderFromPointer(r); return true; }
            for (int c = 0; c <= ControlBindings.KeySlots; c++)
                if (hit == SettingsView.CellId(r, c))
                {
                    (Row, Column) = (r, c);
                    Capturing = true;
                    _captureStarted = Raylib.GetTime();
                    return true;
                }
            if (hit != SettingsView.RowId(r)) continue;
            Row = r;
            switch (Tab)
            {
                case SettingsTab.Sound: UpdateSound(0, confirm: true); break;
                case SettingsTab.Controls: if (r == RowCount) UpdateControls(0, confirm: true, clear: false); break; // only Reset acts
                case SettingsTab.Game: UpdateGame(0, confirm: true); break;
            }
            return true;
        }
        return true; // a click on nothing does nothing
    }

    private void Hover(int hit)
    {
        for (int r = 1; r <= RowCount; r++)
        {
            int column = -1;
            for (int c = 0; c <= ControlBindings.KeySlots; c++) if (hit == SettingsView.CellId(r, c)) column = c;
            bool onRow = column >= 0 || hit == SettingsView.RowId(r) || hit == SettingsView.SliderId(r);
            if (!onRow) continue;
            if (Row != r || (column >= 0 && Column != column)) Tick();
            Row = r;
            if (column >= 0) Column = column;
            return;
        }
    }

    /// <summary>The value range and step of the slider on <paramref name="row"/> of the current tab, or null.</summary>
    private (float Min, float Max, float Step)? SliderRange(int row) => (Tab, row) switch
    {
        (SettingsTab.Sound, 1 or 2 or 3) => (0f, 1f, 0.05f),
        (SettingsTab.Game, 2 or 3) => (0.25f, 3f, 0.25f),
        (SettingsTab.Game, 6) => (0.5f, 1.5f, 0.1f),
        _ => null,
    };

    private void SetSliderFromPointer(int row)
    {
        if (SliderRange(row) is not var (min, max, step)) return;
        float t = Math.Clamp((_mouse.At.X - SettingsView.SliderX) / SettingsView.SliderWidth, 0f, 1f);
        float value = MathF.Round((min + MathF.Round(t * (max - min) / step) * step) * 100) / 100;
        var st = s.Settings;
        float before;
        switch (Tab, row)
        {
            case (SettingsTab.Sound, 1): before = st.MasterVolume; st.MasterVolume = value; break;
            case (SettingsTab.Sound, 2): before = st.MusicVolume; st.MusicVolume = value; break;
            case (SettingsTab.Sound, 3): before = st.EffectsVolume; st.EffectsVolume = value; break;
            case (SettingsTab.Game, 2): before = st.MouseSensitivity; st.MouseSensitivity = value; break;
            case (SettingsTab.Game, 3): before = st.StickSensitivity; st.StickSensitivity = value; break;
            default: before = st.RenderScale; st.RenderScale = value; break;
        }
        if (before == value) return;
        if (Tab == SettingsTab.Sound) s.Audio.ApplySettings(st);
        Tick();
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
            case 4:
                if (step != 0 || confirm)
                    st.CameraFollow = (CameraFollow)(((int)st.CameraFollow + (step < 0 ? 2 : 1)) % 3);
                break;
            case 5: if (step != 0 || confirm) st.SmoothEdges = !st.SmoothEdges; break;
            case 6: st.RenderScale = Nudge(st.RenderScale, step * 0.1f, 0.5f, 1.5f); break;
            case 7:
                if (step != 0 || confirm) st.TutorialHelpers = !st.TutorialHelpers;
                break;
            case 8:
                if (step != 0 || confirm)
                {
                    var rates = Settings.FrameRates;
                    int at = Math.Max(0, Array.IndexOf(rates, st.FrameRate));
                    st.FrameRate = rates[(at + (step < 0 ? rates.Length - 1 : 1)) % rates.Length]; // applied by the game loop
                }
                break;
        }
        if (step != 0 || confirm) Beep();
    }

    private void UpdateControls(int step, bool confirm, bool clear)
    {
        if (step != 0) { Column = Math.Clamp(Column + step, 0, 2); Tick(); }
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
        if (_mouse.Clicked && waited >= CaptureGraceSeconds) cancel = true; // clicking elsewhere gives up on this slot
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
        int tabs = Enum.GetValues<SettingsTab>().Length;
        Tab = (SettingsTab)(((int)Tab + step + tabs) % tabs);
        Row = Math.Min(Row, RowCount);
        Column = 0;
        _dragRow = 0;
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
    private void Tick() => s.Audio.Play(SoundIds.UiMove);

    private static float Nudge(float v, float by, float min, float max) => MathF.Round(Math.Clamp(v + by, min, max) * 100) / 100;
}
