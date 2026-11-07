using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.UI;

/// <summary>Title screen and pause / READY overlays.</summary>
public sealed class MenuView
{
    /// <param name="time">Seconds, for the drifting snow and swaying baubles.</param>
    /// <param name="reducedMotion">Keeps everything still.</param>
    /// <param name="selected">The highlighted item: 0 start, 1 mode, 2 unlocks, 3 settings, 4 quit.</param>
    public void DrawTitle(UiCanvas ui, GameMode mode, int best, int bestRound, double time, bool reducedMotion, int selected,
        IInputService input)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 185));
        Festive.Snowfall(ui, time, moving: !reducedMotion);
        DrawGarland(ui, time, reducedMotion);
        DrawTitleWithCanes(ui, time, reducedMotion);
        ui.Text("You make the toys. Santa takes the credit.", 480, 146, 22, Palette.Gold, Align.Center);
        ui.Text("Finish your shift in Santa's workshop while your boss keeps shifting the rules.", 480, 184, 16, Palette.Snow, Align.Center);

        string modeText = mode == GameMode.Relaxed ? "Relaxed" : "Standard";
        string[] items = ["Start shift", $"Mode:  {modeText}", "Unlocks", "Settings", "Quit"];
        float y = 222;
        for (int i = 0; i < items.Length; i++, y += 44)
            MenuItem(ui, items[i], y, i == selected, arrows: i == 1);
        ui.Text(mode == GameMode.Relaxed ? "Longer deadlines and a quota of 6: a gentler shift." : "Meet the quota to reach the next round. Miss it and the run is over.",
            480, 448, 15, Palette.Ice, Align.Center);
        if (best > 0) ui.Text($"Best ({mode}): {best} points, round {bestRound}", 480, 472, 15, Palette.Shade(Palette.Ice, 0.85f), Align.Center);
        string arrowsHint = input.UsingGamepad ? "D-pad" : "Arrows";
        ui.Text($"{arrowsHint}  Choose      {input.Label(InputAction.Confirm)}  Select      {input.Label(InputAction.Back)}  Quit",
            480, 508, 14, Palette.Shade(Palette.Snow, 0.65f), Align.Center);
    }

    /// <summary>One menu button; the selected one is gold with holly at its ends.</summary>
    private static void MenuItem(UiCanvas ui, string text, float y, bool selected, bool arrows = false)
    {
        const float w = 300, h = 40, x = 480 - w / 2;
        ui.FillRounded(x, y, w, h, selected ? Palette.Gold : Palette.WithAlpha(Palette.Ink, 200), 0.45f);
        if (!selected) ui.OutlineRounded(x, y, w, h, Palette.WithAlpha(Palette.Snow, 70), 1.5f, 0.45f);
        ui.Text(text, 480, y + 9, 22, selected ? Palette.Ink : Palette.Snow, Align.Center);
        if (!selected) return;
        Festive.HollySprig(ui, x + 4, y + 4, 1f, 1f, 0.55f, -0.2f);
        Festive.HollySprig(ui, x + w - 4, y + 4, -1f, 1f, 0.55f, 0.2f);
        if (arrows)
        {
            ui.Text("<", x + 30, y + 9, 22, Palette.Ink);
            ui.Text(">", x + w - 30, y + 9, 22, Palette.Ink, Align.Right);
        }
    }

    /// <summary>The pause menu, with the player's current controls beside it (keyboard and controller).</summary>
    /// <param name="selected">0 resume, 1 restart, 2 settings, 3 quit to title.</param>
    public void DrawPause(UiCanvas ui, int selected, ControlBindings c, IInputService input)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 205));
        ui.Text("PAUSED", 250, 92, 48, Palette.Snow, Align.Center);
        string[] items = ["Resume", "Restart shift", "Settings", "Quit to title"];
        float y = 170;
        for (int i = 0; i < items.Length; i++, y += 54) MenuItemAt(ui, 250, items[i], y, i == selected);
        string arrowsHint = input.UsingGamepad ? "D-pad" : "Arrows";
        ui.Text($"{arrowsHint}  Choose     {input.Label(InputAction.Confirm)}  Select     {input.Label(InputAction.Pause)}  Resume",
            250, 400, 14, Palette.Shade(Palette.Snow, 0.65f), Align.Center);

        // Controls card
        const float px = 470, py = 70, pw = 450, ph = 380;
        ui.FillRounded(px, py, pw, ph, Palette.Hex("#1B232D"), 0.06f);
        ui.OutlineRounded(px, py, pw, ph, Palette.WithAlpha(Palette.Gold, 150), 2, 0.06f);
        Festive.HollySprig(ui, px + pw - 24, py + 22, -1f, 1f, 0.6f);
        ui.Text("CONTROLS", px + 24, py + 16, 24, Palette.Gold);
        ui.Text("Keyboard", px + 230, py + 54, 14, Palette.Ice, Align.Center);
        ui.Text("Controller", px + 370, py + 54, 14, Palette.Ice, Align.Center);

        string K(string a) => InputNames.Key(c.KeyAt(a, 0) != "" ? c.KeyAt(a, 0) : c.KeyAt(a, 1));
        string B(string a) => InputNames.Button(c.PadFor(a));
        (string What, string Keys, string Pad)[] rows =
        [
            ("Move", $"{K(ControlBindings.MoveUp)} {K(ControlBindings.MoveLeft)} {K(ControlBindings.MoveDown)} {K(ControlBindings.MoveRight)}", "Left stick"),
            ("Look around", "Mouse", "Right stick"),
            ("Use / pick up", K(ControlBindings.Interact), B(ControlBindings.Interact)),
            ("Hammer parts", $"{K(ControlBindings.Part1)} {K(ControlBindings.Part2)} {K(ControlBindings.Part3)}",
                $"{B(ControlBindings.Part1)} {B(ControlBindings.Part2)} {B(ControlBindings.Part3)}"),
            ("Pause", "Esc", "Start"),
        ];
        float ry = py + 80;
        foreach (var (what, keys, pad) in rows)
        {
            ui.Line(px + 20, ry - 6, px + pw - 20, ry - 6, 1, Palette.WithAlpha(Palette.Snow, 25));
            ui.Text(what, px + 24, ry + 2, 17, Palette.Snow);
            ui.Text(ui.Fit(keys, 16, 130), px + 230, ry + 3, 16, Palette.Gold, Align.Center);
            ui.Text(ui.Fit(pad, 16, 130), px + 370, ry + 3, 16, Palette.Gold, Align.Center);
            ry += 40;
        }
        ui.Text("At the bench, hammer the parts on in the order shown.", px + pw / 2, ry + 6, 14, Palette.Ice, Align.Center);
        ui.Text("Change any of these in Settings > Controls.", px + pw / 2, ry + 28, 14, Palette.Shade(Palette.Snow, 0.65f), Align.Center);
    }

    private static void MenuItemAt(UiCanvas ui, float cx, string text, float y, bool selected)
    {
        const float w = 280, h = 42;
        float x = cx - w / 2;
        ui.FillRounded(x, y, w, h, selected ? Palette.Gold : Palette.WithAlpha(Palette.Snow, 25), 0.45f);
        ui.Text(text, cx, y + 10, 22, selected ? Palette.Ink : Palette.Snow, Align.Center);
        if (!selected) return;
        Festive.HollySprig(ui, x + 4, y + 4, 1f, 1f, 0.55f, -0.2f);
        Festive.HollySprig(ui, x + w - 4, y + 4, -1f, 1f, 0.55f, 0.2f);
    }

    public void DrawConfirm(UiCanvas ui, string question, IInputService input)
    {
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 220));
        ui.Text(question, 480, 210, 28, Palette.Snow, Align.Center);
        ui.Text($"{input.Label(InputAction.Confirm)}  Yes        {input.Label(InputAction.Back)}  No", 480, 270, 22, Palette.Gold, Align.Center);
    }

    public void DrawReady(UiCanvas ui, float secondsLeft)
    {
        ui.FillRounded(380, 230, 200, 80, Palette.WithAlpha(Palette.Ink, 220), 0.3f);
        ui.Text("READY", 480, 244, 40, Palette.Gold, Align.Center);
        ui.Text($"{Math.Max(0.1f, secondsLeft):0.0}", 480, 286, 16, Palette.Snow, Align.Center);
    }

    private const string Title = "SANTA SHIFT";
    private const float TitleSize = 64, TitleY = 70;

    /// <summary>The title in festive red with a gold shadow, between two candy canes wrapped in holly.</summary>
    private static void DrawTitleWithCanes(UiCanvas ui, double time, bool still)
    {
        float half = ui.MeasureText(Title, TitleSize) / 2;
        float sway = still ? 0 : 0.035f * MathF.Sin((float)time * 1.2f);
        Festive.CandyCane(ui, 480 - half - 44, TitleY - 12, TitleY + 100, hookSide: -1, sway: -sway);
        Festive.CandyCane(ui, 480 + half + 44, TitleY - 12, TitleY + 100, hookSide: 1, sway: sway);

        foreach (var (dx, dy) in new[] { (-2f, 0f), (2f, 0f), (0f, -2f), (0f, 2f) })
            ui.Text(Title, 480 + dx, TitleY + dy, TitleSize, Palette.Shade(Palette.Ink, 0.8f), Align.Center);
        ui.Text(Title, 483, TitleY + 4, TitleSize, Palette.Gold, Align.Center);
        ui.Text(Title, 480, TitleY, TitleSize, Festive.CaneRed, Align.Center);
    }

    /// <summary>A holly garland swagged along the top edge, with glass baubles hanging from where it is pinned up.</summary>
    private static void DrawGarland(UiCanvas ui, double time, bool still)
    {
        const float span = 160, dip = 22;
        for (float x0 = 0; x0 < UiCanvas.Width; x0 += span)
        {
            static float Sag(float t) => 4 + dip * 4 * t * (1 - t);
            for (float t = 0; t < 1; t += 0.05f) // the rope the leaves are tied along
                ui.Line(x0 + t * span, Sag(t), x0 + (t + 0.05f) * span, Sag(t + 0.05f), 4, Festive.HollyDark);
            int k = 0;
            for (float t = 0.04f; t < 1; t += 0.12f, k++)
            {
                float x = x0 + t * span, y = Sag(t);
                float side = k % 2 == 0 ? -1 : 1;
                var dir = Vector2.Normalize(new Vector2(0.9f + 0.25f * side, 0.55f * side + 0.15f));
                Festive.HollyLeaf(ui, new Vector2(x, y), dir, 24, 7.5f, k % 3 == 0 ? Festive.HollyLight : Festive.Holly, Festive.HollyDark);
            }
        }
        for (float x0 = span; x0 < UiCanvas.Width; x0 += span)
        {
            Festive.Berry(ui, x0 - 4, 7, 4.5f);
            Festive.Berry(ui, x0 + 4, 9, 4.5f);
            Festive.Berry(ui, x0, 13, 4.5f);
        }

        // Baubles hang at the sides, clear of the title.
        (float X, float Thread, float R, Color C, int Style)[] baubles =
        [
            (36, 92, 17, Palette.Berry, 1), (112, 46, 13, Palette.Gold, 2), (178, 132, 15, Festive.HollyLight, 3),
            (782, 124, 15, Palette.Ice, 2), (850, 52, 14, Palette.Berry, 3), (922, 100, 17, Palette.Gold, 1),
        ];
        for (int i = 0; i < baubles.Length; i++)
        {
            var b = baubles[i];
            float swing = still ? 0 : 0.06f * MathF.Sin((float)time * (1.1f + i * 0.13f) + i * 1.7f);
            Festive.Ornament(ui, b.X, b.Thread, b.R, b.C, b.Style, swing);
        }
    }

}
