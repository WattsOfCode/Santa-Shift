using SantaShift.Core.Models;

namespace SantaShift.Game.Views.UI;

/// <summary>Title screen and pause / READY overlays.</summary>
public sealed class MenuView
{
    public void DrawTitle(UiCanvas ui, GameMode mode, int best)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 190));
        ui.Text("SANTA SHIFT", 480, 70, 64, Palette.Berry, Align.Center);
        ui.Text("You make the toys. Santa takes the credit.", 480, 146, 22, Palette.Gold, Align.Center);
        ui.Text("Finish your shift in Santa's workshop while your boss keeps shifting the rules.", 480, 184, 16, Palette.Snow, Align.Center);

        float y = 236;
        Line(ui, "WASD / Arrows", "Move", y);
        Line(ui, "E / Space", "Collect, assemble, ribbon, ship, recycle", y += 30);
        Line(ui, "Esc", "Pause", y += 30);

        ui.FillRounded(330, 352, 300, 44, Palette.Gold, 0.4f);
        ui.Text("ENTER  Start shift", 480, 363, 22, Palette.Ink, Align.Center);

        string modeText = mode == GameMode.Relaxed ? "Relaxed (longer deadlines, quota 6)" : "Standard";
        ui.Text($"TAB  Mode: {modeText}", 480, 414, 18, Palette.Snow, Align.Center);
        if (best > 0) ui.Text($"Best ({mode}): {best}", 480, 442, 16, Palette.Ice, Align.Center);
        ui.Text("ESC  Quit", 480, 500, 16, Palette.Shade(Palette.Snow, 0.7f), Align.Center);
    }

    public void DrawPause(UiCanvas ui)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 200));
        ui.Text("PAUSED", 480, 170, 48, Palette.Snow, Align.Center);
        ui.Text("ENTER / ESC   Resume", 480, 250, 22, Palette.Gold, Align.Center);
        ui.Text("R   Restart shift", 480, 290, 20, Palette.Snow, Align.Center);
        ui.Text("Q   Quit to title", 480, 322, 20, Palette.Snow, Align.Center);
    }

    public void DrawConfirm(UiCanvas ui, string question)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 220));
        ui.Text(question, 480, 210, 28, Palette.Snow, Align.Center);
        ui.Text("ENTER  Yes        ESC  No", 480, 270, 22, Palette.Gold, Align.Center);
    }

    public void DrawReady(UiCanvas ui, float secondsLeft)
    {
        ui.FillRounded(380, 230, 200, 80, Palette.WithAlpha(Palette.Ink, 220), 0.3f);
        ui.Text("READY", 480, 244, 40, Palette.Gold, Align.Center);
        ui.Text($"{Math.Max(0.1f, secondsLeft):0.0}", 480, 286, 16, Palette.Snow, Align.Center);
    }

    private static void Line(UiCanvas ui, string key, string action, float y)
    {
        ui.Text(key, 470, y, 18, Palette.Gold, Align.Right);
        ui.Text(action, 490, y, 18, Palette.Snow);
    }
}
