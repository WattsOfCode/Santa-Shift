using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.UI;

/// <summary>Draws the unlocks list: what each one gives, progress towards it, and which hat the elf is wearing.</summary>
public sealed class UnlocksView
{
    private const float X = 150, Y = 30, W = 660, H = 480;

    public void Draw(UiCanvas ui, IReadOnlyList<Unlock> unlocks, PlayerProgress p, int row, IInputService input)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 210));
        ui.FillRounded(X, Y, W, H, Palette.Hex("#1B232D"), 0.05f);
        ui.OutlineRounded(X, Y, W, H, Palette.WithAlpha(Palette.Gold, 160), 2, 0.05f);
        Festive.HollySprig(ui, X + 26, Y + 24, 1f, 1f, 0.7f);
        Festive.HollySprig(ui, X + W - 26, Y + 24, -1f, 1f, 0.7f);
        ui.Text("UNLOCKS", X + W / 2, Y + 12, 30, Palette.Gold, Align.Center);
        ui.Text($"{p.ToysDelivered} toys and {p.CoalDelivered} coal delivered   -   furthest round {p.BestRound}   -   {p.RunsPlayed} runs",
            X + W / 2, Y + 50, 14, Palette.Ice, Align.Center);

        float y = Y + 78;
        const float rowH = 40;
        DrawRow(ui, "Classic Red Hat", "The elf's own hat.", true, 1, 1, "", row == 0, p.Hat == "", Palette.Hex("#BA2D33"), y);
        for (int i = 0; i < unlocks.Count; i++)
        {
            var u = unlocks[i];
            y += rowH;
            bool have = p.Has(u.Id);
            var swatch = u.Reward == UnlockReward.Hat ? Palette.Hex(u.Value) : (Raylib_cs.Color?)null;
            DrawRow(ui, u.Name, u.Description, have, p.Value(u.Stat), u.Target, StatName(u.Stat), row == i + 1,
                u.Reward == UnlockReward.Hat && p.Hat == u.Id, swatch, y);
        }

        ui.Text($"{(input.UsingGamepad ? "D-pad" : "Arrows")}  Choose      {input.Label(InputAction.Confirm)}  Wear hat      " +
                $"{input.Label(InputAction.Back)}  Back", X + W / 2, Y + H - 28, 15, Palette.Ice, Align.Center);
    }

    private static void DrawRow(UiCanvas ui, string name, string text, bool have, int value, int target, string stat,
        bool selected, bool wearing, Raylib_cs.Color? swatch, float y)
    {
        if (selected) ui.FillRounded(X + 18, y - 4, W - 36, 36, Palette.WithAlpha(Palette.Gold, 40), 0.3f);
        float tx = X + 40;
        if (swatch is { } c) // a hat colour
        {
            ui.Triangle(new(tx, y + 26), new(tx + 22, y + 26), new(tx + 11, y + 2), have ? c : Palette.WithAlpha(c, 70));
            ui.Circle(tx + 11, y + 2, 4, Palette.Snow);
            ui.FillRounded(tx - 2, y + 24, 26, 6, Palette.Snow, 0.5f);
        }
        else Festive.Berry(ui, tx + 11, y + 14, 8, have ? 1f : 0.35f);

        ui.Text(name, tx + 36, y, 18, have ? (selected ? Palette.Gold : Palette.Snow) : Palette.Shade(Palette.Snow, 0.55f));
        ui.Text(ui.Fit(text, 13, 300), tx + 36, y + 19, 13, Palette.Shade(Palette.Snow, have ? 0.75f : 0.5f));

        float bx = X + W - 270, bw = 130;
        if (wearing) ui.Text("WEARING", X + W - 40, y + 6, 15, Palette.Gold, Align.Right);
        else if (have) ui.Text("UNLOCKED", X + W - 40, y + 6, 15, Palette.Pine, Align.Right);
        else
        {
            float frac = Math.Clamp(value / (float)Math.Max(1, target), 0, 1);
            ui.FillRounded(bx, y + 8, bw, 8, Palette.WithAlpha(Palette.Snow, 35), 0.6f);
            ui.FillRounded(bx, y + 8, bw * frac, 8, Palette.Ice, 0.6f);
            ui.Text($"{Math.Min(value, target)} / {target} {stat}", bx + bw + 4, y + 4, 12, Palette.Shade(Palette.Snow, 0.7f));
        }
    }

    private static string StatName(UnlockStat s) => s switch
    {
        UnlockStat.ToysDelivered => "toys",
        UnlockStat.CoalDelivered => "coal",
        UnlockStat.BestRound => "round",
        UnlockStat.RoundsCleared => "rounds",
        UnlockStat.RunsPlayed => "runs",
        _ => "",
    };
}
