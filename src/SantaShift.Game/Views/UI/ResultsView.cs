using SantaShift.Game.Services;
using SantaShift.Core.Models;

namespace SantaShift.Game.Views.UI;

/// <summary>Everything the results screen shows. Built from the real run, so Santa can't contradict it.</summary>
public sealed record ResultsSummary(
    RunProgress Run, bool Passed, string Heading, string ReviewLabel, string SantaLine,
    int Delivered, int Quota, int Missed, int Unfinished, int RoundScore, int BestStreak,
    int NextQuota, int BestScore, int BestRound, bool NewBest, IReadOnlyList<string> Unlocked, double Spirit);

/// <summary>End of a round: "Round complete" (continue) or "Game over" (run finished).</summary>
public sealed class ResultsView
{
    public void Draw(UiCanvas ui, ResultsSummary r, IInputService input)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 215));
        ui.Text(r.Heading, 480, 40, 48, r.Passed ? Palette.Gold : Palette.Berry, Align.Center);
        ui.Text(r.ReviewLabel, 480, 98, 26, Palette.Snow, Align.Center);
        ui.Text($"\"{r.SantaLine}\"  - Santa", 480, 136, 16, Palette.Ice, Align.Center);

        float y = 180;
        Row(ui, "Delivered", $"{r.Delivered} / {r.Quota}", y);
        Row(ui, "Missed", r.Missed.ToString(), y += 27);
        Row(ui, "Unfinished", r.Unfinished.ToString(), y += 27);
        Row(ui, "Best streak", r.BestStreak.ToString(), y += 27);
        Row(ui, "Round score", r.RoundScore.ToString(), y += 27);
        Row(ui, "Run total", r.Run.TotalScore.ToString(), y += 27);
        Row(ui, "Christmas spirit", $"{r.Spirit:0}%", y += 27);

        // New unlocks this round, in a gold ribbon across the screen.
        if (r.Unlocked.Count > 0)
        {
            string text = "Unlocked: " + string.Join(",  ", r.Unlocked) + "!";
            float w = ui.MeasureText(text, 18) + 40;
            ui.FillRounded(480 - w / 2, 404, w, 30, Palette.Gold, 0.5f);
            Festive.HollySprig(ui, 480 - w / 2 + 4, 406, 1f, 1f, 0.5f);
            Festive.HollySprig(ui, 480 + w / 2 - 4, 406, -1f, 1f, 0.5f);
            ui.Text(text, 480, 410, 18, Palette.Ink, Align.Center);
        }

        if (r.Passed)
        {
            ui.Text($"Round {r.Run.Round + 1}: quota {r.NextQuota}, shorter deadlines.", 480, y += 34, 18, Palette.Gold, Align.Center);
            Button(ui, $"{input.Label(InputAction.Confirm)}  Next round", 442);
            ui.Text($"{input.Label(InputAction.Back)}  End run and return to title", 480, 494, 16, Palette.Snow, Align.Center);
        }
        else
        {
            string best = r.NewBest ? $"NEW BEST: {r.BestScore}" : $"Best: {r.BestScore}";
            ui.Text($"Reached round {r.Run.Round}.  {best}  (furthest round {r.BestRound})", 480, y += 34, 18,
                r.NewBest ? Palette.Gold : Palette.Ice, Align.Center);
            Button(ui, $"{input.Label(InputAction.Confirm)}  New run", 442);
            ui.Text($"{input.Label(InputAction.Back)}  Title", 480, 494, 16, Palette.Snow, Align.Center);
        }
    }

    private static void Button(UiCanvas ui, string text, float y)
    {
        ui.FillRounded(330, y, 300, 44, Palette.Gold, 0.4f);
        ui.Text(text, 480, y + 11, 22, Palette.Ink, Align.Center);
    }

    private static void Row(UiCanvas ui, string label, string value, float y)
    {
        ui.Text(label, 470, y, 20, Palette.Shade(Palette.Snow, 0.8f), Align.Right);
        ui.Text(value, 490, y, 20, Palette.Snow);
    }
}
