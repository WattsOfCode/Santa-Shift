using SantaShift.Core.Models;

namespace SantaShift.Game.Views.UI;

/// <summary>Everything the results screen shows. Built from the real run, so Santa can't contradict it.</summary>
public sealed record ResultsSummary(
    GameMode Mode, bool Passed, string Heading, string ReviewLabel, string SantaLine,
    int Delivered, int Quota, int Missed, int Unfinished, int Score, int BestStreak, int BestScore, bool NewBest);

/// <summary>The performance review (GDD section 12).</summary>
public sealed class ResultsView
{
    public void Draw(UiCanvas ui, ResultsSummary r)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 215));
        ui.Text(r.Heading, 480, 48, 48, r.Passed ? Palette.Gold : Palette.Berry, Align.Center);
        ui.Text(r.ReviewLabel, 480, 106, 26, Palette.Snow, Align.Center);
        ui.Text($"\"{r.SantaLine}\"  - Santa", 480, 144, 16, Palette.Ice, Align.Center);

        float y = 196;
        Row(ui, "Delivered", $"{r.Delivered} / {r.Quota}", y);
        Row(ui, "Missed", r.Missed.ToString(), y += 32);
        Row(ui, "Unfinished", r.Unfinished.ToString(), y += 32);
        Row(ui, "Best streak", r.BestStreak.ToString(), y += 32);
        Row(ui, "Score", r.Score.ToString(), y += 32);
        Row(ui, $"Best ({r.Mode})", r.NewBest ? $"{r.BestScore}  NEW!" : r.BestScore.ToString(), y += 32);

        ui.FillRounded(330, 420, 300, 44, Palette.Gold, 0.4f);
        ui.Text("ENTER  Play again", 480, 431, 22, Palette.Ink, Align.Center);
        ui.Text("ESC  Title", 480, 482, 18, Palette.Snow, Align.Center);
    }

    private static void Row(UiCanvas ui, string label, string value, float y)
    {
        ui.Text(label, 470, y, 22, Palette.Shade(Palette.Snow, 0.8f), Align.Right);
        ui.Text(value, 490, y, 22, Palette.Snow);
    }
}
