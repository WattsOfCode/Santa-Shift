using SantaShift.Core.Models;
using SantaShift.Game.Controllers;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.UI;

/// <summary>Draws the settings screen (see <see cref="SettingsMenu"/>): a panel with three tabs.</summary>
public sealed class SettingsView
{
    private const float X = 150, Y = 46, W = 660, H = 448;
    private const float RowsTop = Y + 112, RowH = 30;

    private static readonly Dictionary<string, string> ActionNames = new()
    {
        [ControlBindings.MoveUp] = "Move up", [ControlBindings.MoveDown] = "Move down",
        [ControlBindings.MoveLeft] = "Move left", [ControlBindings.MoveRight] = "Move right",
        [ControlBindings.Interact] = "Use / pick up", [ControlBindings.Part1] = "Part 1",
        [ControlBindings.Part2] = "Part 2", [ControlBindings.Part3] = "Part 3",
        [ControlBindings.CameraLeft] = "Turn camera left", [ControlBindings.CameraRight] = "Turn camera right",
    };

    public void Draw(UiCanvas ui, SettingsMenu m)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 210));
        ui.FillRounded(X, Y, W, H, Palette.Hex("#1B232D"), 0.05f);
        ui.OutlineRounded(X, Y, W, H, Palette.WithAlpha(Palette.Gold, 160), 2, 0.05f);
        Festive.HollySprig(ui, X + 26, Y + 24, 1f, 1f, 0.7f);
        Festive.HollySprig(ui, X + W - 26, Y + 24, -1f, 1f, 0.7f);
        ui.Text("SETTINGS", X + W / 2, Y + 14, 30, Palette.Gold, Align.Center);

        // Tabs
        string[] tabs = ["Sound", "Controls", "Game", "Credits"];
        const float tw = 136, tgap = 10;
        float tx = X + W / 2 - (tabs.Length * tw + (tabs.Length - 1) * tgap) / 2;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool current = (int)m.Tab == i, focused = current && m.Row == 0;
            float x = tx + i * (tw + tgap);
            ui.FillRounded(x, Y + 58, tw, 32, current ? Palette.Berry : Palette.WithAlpha(Palette.Snow, 30), 0.4f);
            if (focused) ui.OutlineRounded(x - 3, Y + 55, tw + 6, 38, Palette.Gold, 2, 0.4f);
            ui.Text(tabs[i], x + tw / 2, Y + 64, 20, current ? Palette.Snow : Palette.Shade(Palette.Snow, 0.75f), Align.Center);
        }

        switch (m.Tab)
        {
            case SettingsTab.Sound: DrawSound(ui, m); break;
            case SettingsTab.Controls: DrawControls(ui, m); break;
            case SettingsTab.Game: DrawGame(ui, m); break;
            case SettingsTab.Credits: DrawCredits(ui, m); break;
        }

        var input = m.Input;
        string hint = m.Capturing
            ? (m.Column < ControlBindings.KeySlots ? "Press the new key      Esc  Cancel" : "Press the new button      Start  Cancel")
            : $"{Arrows(input)}  Choose      {input.Label(InputAction.Confirm)}  Select      " +
              $"{input.Label(InputAction.TabNext)}  Next tab      {input.Label(InputAction.Back)}  Save and close";
        ui.Text(hint, X + W / 2, Y + H - 30, 15, Palette.Ice, Align.Center);
    }

    private static string Arrows(IInputService input) => input.UsingGamepad ? "D-pad" : "Arrows";

    private static void Row(UiCanvas ui, SettingsMenu m, int row, string label)
    {
        float y = RowsTop + (row - 1) * RowH;
        bool selected = m.Row == row;
        if (selected) ui.FillRounded(X + 20, y - 4, W - 40, RowH - 2, Palette.WithAlpha(Palette.Gold, 45), 0.3f);
        ui.Text(label, X + 40, y, 18, selected ? Palette.Gold : Palette.Snow);
    }

    private static void Slider(UiCanvas ui, SettingsMenu m, int row, string label, float value, float min, float max, string text)
    {
        Row(ui, m, row, label);
        float y = RowsTop + (row - 1) * RowH, bx = X + 290, bw = 250;
        float t = (value - min) / (max - min);
        ui.FillRounded(bx, y + 5, bw, 10, Palette.WithAlpha(Palette.Snow, 40), 0.5f);
        ui.FillRounded(bx, y + 5, bw * t, 10, m.Row == row ? Palette.Gold : Palette.Ice, 0.5f);
        ui.Circle(bx + bw * t, y + 10, 8, m.Row == row ? Palette.Gold : Palette.Snow);
        ui.Text(text, X + W - 40, y, 18, Palette.Snow, Align.Right);
    }

    private static void Toggle(UiCanvas ui, SettingsMenu m, int row, string label, bool on)
    {
        Row(ui, m, row, label);
        float y = RowsTop + (row - 1) * RowH;
        ui.FillRounded(X + W - 110, y - 1, 70, 22, on ? Palette.Pine : Palette.WithAlpha(Palette.Snow, 40), 0.5f);
        ui.Text(on ? "On" : "Off", X + W - 75, y + 1, 18, Palette.Snow, Align.Center);
    }

    private static void Choice(UiCanvas ui, SettingsMenu m, int row, string label, string value)
    {
        Row(ui, m, row, label);
        float y = RowsTop + (row - 1) * RowH;
        bool on = m.Row == row;
        ui.Text(on ? $"<  {value}  >" : value, X + W - 40, y, 18, on ? Palette.Gold : Palette.Snow, Align.Right);
    }

    private static void DrawSound(UiCanvas ui, SettingsMenu m)
    {
        var st = m.Settings;
        Slider(ui, m, 1, "Master volume", st.MasterVolume, 0, 1, $"{st.MasterVolume * 100:0}%");
        Slider(ui, m, 2, "Music", st.MusicVolume, 0, 1, $"{st.MusicVolume * 100:0}%");
        Slider(ui, m, 3, "Sound effects", st.EffectsVolume, 0, 1, $"{st.EffectsVolume * 100:0}%");
        Toggle(ui, m, 4, "Mute everything", st.Muted);
        ui.Text("Music: \"Toy Factory Jingle\" and \"Cozy Christmas Game Ambience\" by blueeyedmonkey",
            X + W / 2, RowsTop + 5 * RowH, 13, Palette.Shade(Palette.Snow, 0.6f), Align.Center);
    }

    private static void DrawGame(UiCanvas ui, SettingsMenu m)
    {
        var st = m.Settings;
        Toggle(ui, m, 1, "Reduced motion (no sway, flicker or falling snow)", st.ReducedMotion);
        Slider(ui, m, 2, "Mouse look speed", st.MouseSensitivity, 0.25f, 3, $"x{st.MouseSensitivity:0.00}");
        Slider(ui, m, 3, "Controller look speed", st.StickSensitivity, 0.25f, 3, $"x{st.StickSensitivity:0.00}");
        Choice(ui, m, 4, "Camera follows the elf", st.CameraFollow switch
        {
            CameraFollow.Always => "Always (good on a trackpad)",
            CameraFollow.Off => "Off",
            _ => "After a moment",
        });
        Toggle(ui, m, 5, "Smooth jagged edges", st.SmoothEdges);
        Slider(ui, m, 6, "3D resolution", st.RenderScale, 0.5f, 1.5f, $"{st.RenderScale * 100:0}%");
        Choice(ui, m, 7, "Tutorial guide in round 1", st.Guide switch
        {
            GuideMode.EveryRun => "Every run",
            GuideMode.Off => "Off",
            _ => "Until you clear a round",
        });
        Choice(ui, m, 8, "Frame rate", st.FrameRate switch
        {
            Settings.MatchDisplay => "Match display (VSync)",
            Settings.Unlimited => "Unlimited",
            var fps => $"{fps} fps",
        });
    }

    /// <summary>Settings > Credits: credits.json in two columns, with the copyright footer across the bottom.</summary>
    private static void DrawCredits(UiCanvas ui, SettingsMenu m)
    {
        var credits = m.Content.Credits;
        if (credits.IsEmpty)
        {
            ui.Text("credits.json is missing.", X + W / 2, RowsTop + 40, 18, Palette.Snow, Align.Center);
            return;
        }
        float colW = (W - 100) / 2;
        Column(credits.Left, X + 40);
        Column(credits.Right, X + 60 + colW);

        float fy = Y + H - 82;
        ui.Fill(X + 40, fy - 8, W - 80, 1, Palette.WithAlpha(Palette.Gold, 90));
        foreach (var line in credits.Footer)
        {
            ui.Text(ui.Fit(line, 13, W - 60), X + W / 2, fy, 13, Palette.Shade(Palette.Snow, 0.75f), Align.Center);
            fy += 17;
        }

        void Column(List<Core.Models.CreditSection> sections, float x)
        {
            float y = RowsTop - 8;
            foreach (var section in sections)
            {
                ui.Text(section.Heading, x, y, 15, Palette.Gold);
                y += 20;
                foreach (var line in section.Lines)
                {
                    ui.Text(ui.Fit(line, 15, colW), x, y, 15, Palette.Snow);
                    y += 18;
                }
                y += 8;
            }
        }
    }

    private static void DrawControls(UiCanvas ui, SettingsMenu m)
    {
        var c = m.Settings.Controls;
        float[] colX = [X + 300, X + 420, X + 540];
        const float cellW = 108;
        string[] heads = ["Key", "Key 2", "Controller"];
        for (int i = 0; i < 3; i++) ui.Text(heads[i], colX[i] + cellW / 2, RowsTop - 22, 13, Palette.Ice, Align.Center);

        var actions = ControlBindings.Actions;
        const float rowH = 23;
        for (int r = 0; r < actions.Length; r++)
        {
            int row = r + 1;
            float y = RowsTop + r * rowH;
            bool selected = m.Row == row;
            if (selected) ui.FillRounded(X + 20, y - 3, W - 40, rowH - 2, Palette.WithAlpha(Palette.Gold, 35), 0.3f);
            ui.Text(ActionNames[actions[r]], X + 40, y, 17, selected ? Palette.Gold : Palette.Snow);
            for (int col = 0; col < 3; col++)
            {
                bool cell = selected && m.Column == col;
                string text = cell && m.Capturing ? "Press..."
                    : col < ControlBindings.KeySlots ? InputNames.Key(c.KeyAt(actions[r], col)) : InputNames.Button(c.PadFor(actions[r]));
                ui.FillRounded(colX[col], y - 2, cellW, rowH - 4, cell ? (m.Capturing ? Palette.Berry : Palette.Gold) : Palette.WithAlpha(Palette.Snow, 28), 0.35f);
                ui.Text(ui.Fit(text, 15, cellW - 8), colX[col] + cellW / 2, y + 1, 15, cell && !m.Capturing ? Palette.Ink : Palette.Snow, Align.Center);
            }
        }

        int reset = actions.Length + 1;
        float ry = RowsTop + actions.Length * rowH + 4;
        bool onReset = m.Row == reset;
        ui.FillRounded(X + W / 2 - 100, ry - 2, 200, 24, onReset ? Palette.Gold : Palette.WithAlpha(Palette.Snow, 30), 0.4f);
        ui.Text("Reset to defaults", X + W / 2, ry + 1, 16, onReset ? Palette.Ink : Palette.Snow, Align.Center);

        var input = m.Input;
        ui.Text($"Always: Esc / Start pause    Enter / A confirm    {input.Label(InputAction.ClearBinding)} clears a slot",
            X + W / 2, ry + 32, 13, Palette.Shade(Palette.Snow, 0.65f), Align.Center);
    }
}
