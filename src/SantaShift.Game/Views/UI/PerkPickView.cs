using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.UI;

/// <summary>The perk cards between rounds: an icon, name and effect on each, the run's perks so far underneath.</summary>
public sealed class PerkPickView
{
    private const float CardW = 196, CardH = 250, Gap = 22, CardY = 150;

    private static float CardX(int i, int count) => UiCanvas.Width / 2 - (count * CardW + (count - 1) * Gap) / 2 + i * (CardW + Gap);

    /// <summary>The card under a UI point, or -1.</summary>
    public int CardAt(Vector2 p, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float x = CardX(i, count);
            if (p.X >= x && p.X <= x + CardW && p.Y >= CardY && p.Y <= CardY + CardH) return i;
        }
        return -1;
    }

    public void Draw(UiCanvas ui, DialogueBook d, RunProgress run, IReadOnlyList<Perk> offers, int selected, bool extraCard,
        bool headStart, int nextQuota, IInputService input, double time, bool still)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 200));
        Festive.Snowfall(ui, time, 50, moving: !still);
        string title = d.Message(headStart ? "head_start" : "perk_title");
        float half = ui.MeasureText(title, 34) / 2;
        Festive.HollySprig(ui, 480 - half - 34, 58, 1f, 1f, 0.9f);
        Festive.HollySprig(ui, 480 + half + 34, 58, -1f, 1f, 0.9f);
        ui.Text(title, 480, 42, 34, Palette.Gold, Align.Center);
        ui.Text(headStart ? $"Round 1 needs {nextQuota} deliveries." : $"Round {run.Round} cleared. Round {run.Round + 1} needs {nextQuota} deliveries.",
            480, 88, 18, Palette.Snow, Align.Center);
        if (extraCard) ui.Text(d.Message("perk_extra"), 480, 114, 16, Palette.Ice, Align.Center);

        for (int i = 0; i < offers.Count; i++)
        {
            var perk = offers[i];
            bool on = i == selected;
            float x = CardX(i, offers.Count), y = CardY - (on ? 10 : 0);
            ui.FillRounded(x + 5, y + 7, CardW, CardH, Palette.WithAlpha(Color.Black, 90), 0.12f);
            ui.FillRounded(x, y, CardW, CardH, on ? Palette.Snow : Palette.Hex("#D9CFB8"), 0.12f);
            ui.OutlineRounded(x, y, CardW, CardH, on ? Palette.Gold : Palette.WithAlpha(Palette.Ink, 80), on ? 4 : 2, 0.12f);
            ui.FillRounded(x + 14, y + 14, CardW - 28, 104, Palette.Hex("#22303C"), 0.15f);
            DrawIcon(ui, perk.Effect, x + CardW / 2, y + 66);
            float nameSize = ui.MeasureText(perk.Name, 20) <= CardW - 16 ? 20 : 17;
            ui.Text(ui.Fit(perk.Name, nameSize, CardW - 12), x + CardW / 2, y + 133, nameSize, Palette.Ink, Align.Center);
            Wrap(ui, perk.Description, x + CardW / 2, y + 162, CardW - 26, 15, Palette.Shade(Palette.Ink, 1.5f));
            int have = run.Stacks(perk.Id);
            string stacks = have > 0 ? $"You have {have} of {perk.MaxStacks}" : $"Up to {perk.MaxStacks}";
            ui.Text(stacks, x + CardW / 2, y + CardH - 26, 13, Palette.Shade(Palette.Ink, 1.9f), Align.Center);
        }

        // The run's perks so far
        var owned = run.Perks.GroupBy(p => p.Id).Select(g => g.Count() > 1 ? $"{g.First().Name} x{g.Count()}" : g.First().Name).ToList();
        ui.Text(owned.Count == 0 ? "Your first perk of the run!" : "Your perks: " + string.Join(",  ", owned),
            480, 432, 15, Palette.Ice, Align.Center);

        string arrows = input.UsingGamepad ? "D-pad" : "Arrows / mouse";
        ui.Text($"{arrows}  Choose      {input.Label(InputAction.Confirm)}  Take it and start the next round",
            480, 500, 15, Palette.Shade(Palette.Snow, 0.7f), Align.Center);
    }

    private static void Wrap(UiCanvas ui, string text, float cx, float y, float width, float size, Color c)
    {
        string line = "";
        foreach (var word in text.Split(' '))
        {
            string next = line.Length == 0 ? word : line + " " + word;
            if (ui.MeasureText(next, size) > width && line.Length > 0)
            {
                ui.Text(line, cx, y, size, c, Align.Center);
                y += size + 4;
                line = word;
            }
            else line = next;
        }
        if (line.Length > 0) ui.Text(line, cx, y, size, c, Align.Center);
    }

    /// <summary>A small picture for each kind of perk, from simple shapes.</summary>
    private static void DrawIcon(UiCanvas ui, PerkEffect effect, float cx, float cy)
    {
        switch (effect)
        {
            case PerkEffect.WalkSpeed: // a pointy elf boot with a bell and speed lines
                ui.FillRounded(cx - 22, cy - 26, 22, 36, Palette.Pine, 0.3f);
                ui.FillRounded(cx - 26, cy + 4, 50, 18, Palette.Pine, 0.6f);
                ui.Triangle(new Vector2(cx + 20, cy + 4), new Vector2(cx + 38, cy - 8), new Vector2(cx + 24, cy + 20), Palette.Pine);
                ui.Circle(cx + 38, cy - 8, 5, Palette.Gold);
                for (int i = 0; i < 3; i++) ui.Line(cx - 52, cy - 12 + i * 12, cx - 32, cy - 12 + i * 12, 3, Palette.Ice);
                break;
            case PerkEffect.BenchTime: // a hammer and a clock face
                ui.Circle(cx + 14, cy, 26, Palette.Snow);
                ui.Line(cx + 14, cy, cx + 14, cy - 16, 3, Palette.Ink);
                ui.Line(cx + 14, cy, cx + 26, cy + 6, 3, Palette.Ink);
                ui.Line(cx - 34, cy + 26, cx - 8, cy - 10, 6, Palette.Wood);
                ui.FillRounded(cx - 22, cy - 26, 30, 16, Palette.Shade(Palette.Snow, 0.6f), 0.3f);
                break;
            case PerkEffect.RibbonSpeed: // a bow
                ui.Triangle(new Vector2(cx, cy), new Vector2(cx - 34, cy - 20), new Vector2(cx - 34, cy + 20), Festive.CaneRed);
                ui.Triangle(new Vector2(cx, cy), new Vector2(cx + 34, cy - 20), new Vector2(cx + 34, cy + 20), Festive.CaneRed);
                ui.Line(cx - 4, cy + 4, cx - 14, cy + 32, 6, Festive.CaneRed);
                ui.Line(cx + 4, cy + 4, cx + 14, cy + 32, 6, Festive.CaneRed);
                ui.Circle(cx, cy, 9, Palette.Gold);
                break;
            case PerkEffect.ShovelSpeed: // a shovel full of coal
                ui.Line(cx - 30, cy - 30, cx + 6, cy + 6, 6, Palette.Wood);
                ui.Triangle(new Vector2(cx - 2, cy + 2), new Vector2(cx + 34, cy + 4), new Vector2(cx + 10, cy + 32), Palette.Shade(Palette.Snow, 0.55f));
                ui.Circle(cx + 14, cy + 8, 7, Palette.Hex("#26262B"));
                ui.Circle(cx + 22, cy + 14, 6, Palette.Hex("#1C1C20"));
                break;
            case PerkEffect.SupplyKits: // a stack of crates with a plus
                ui.FillRounded(cx - 34, cy - 4, 32, 28, Palette.Hex("#6B4A2C"), 0.15f);
                ui.FillRounded(cx + 2, cy - 4, 32, 28, Palette.Hex("#6B4A2C"), 0.15f);
                ui.FillRounded(cx - 16, cy - 32, 32, 28, Palette.Hex("#7C5634"), 0.15f);
                ui.Fill(cx - 3, cy - 32, 6, 28, Palette.Pine);
                ui.Text("+", cx + 30, cy - 40, 30, Palette.Gold);
                break;
            case PerkEffect.Deadlines: // a reindeer-ish antlered clock
                ui.Circle(cx, cy + 4, 26, Palette.Snow);
                ui.Line(cx, cy + 4, cx, cy - 12, 3, Palette.Ink);
                ui.Line(cx, cy + 4, cx + 14, cy + 4, 3, Palette.Ink);
                ui.Line(cx - 16, cy - 18, cx - 30, cy - 38, 4, Palette.Wood);
                ui.Line(cx - 24, cy - 28, cx - 36, cy - 28, 4, Palette.Wood);
                ui.Line(cx + 16, cy - 18, cx + 30, cy - 38, 4, Palette.Wood);
                ui.Line(cx + 24, cy - 28, cx + 36, cy - 28, 4, Palette.Wood);
                break;
            case PerkEffect.ScoreBonus: // a golden star
                var pts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = -MathF.PI / 2 + i * MathF.PI / 5, r = i % 2 == 0 ? 34 : 14;
                    pts[i] = new Vector2(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r);
                }
                for (int i = 0; i < 10; i++) ui.Triangle(new Vector2(cx, cy), pts[i], pts[(i + 1) % 10], Palette.Gold);
                break;
        }
    }
}
