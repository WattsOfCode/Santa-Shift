using SantaShift.Game.Services;
using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Game.Views.UI;

/// <summary>
/// Santa's naughty and nice list on an old parchment scroll: it unrolls between two wooden rods with candy-cane
/// knobs, with holly sprigs, the nice children ticked off in green, the naughty ones in red under a "COAL!" stamp,
/// and Santa's wax seal. Drawn entirely from shapes, no textures.
/// </summary>
public sealed class NaughtyNiceListView(GameContent content)
{
    private static readonly Color Parchment = Palette.Hex("#D9BC82");
    private static readonly Color ParchmentLight = Palette.Hex("#E8D3A0");
    private static readonly Color ParchmentCentre = Palette.Hex("#F0E0B6");
    private static readonly Color ParchmentEdge = Palette.Hex("#A9773E");
    private static readonly Color Scorch = Palette.Hex("#7E5228");
    private static readonly Color Stain = Palette.WithAlpha(Palette.Hex("#A87B40"), 45);
    private static readonly Color InkBrown = Palette.Hex("#4B3324");
    private static readonly Color Rod = Palette.Hex("#7A4A23");
    private static readonly Color RodLight = Palette.Hex("#A8713D");
    private static readonly Color Coal = Palette.Hex("#26262B");

    private const float W = 540, FullH = 430, X = (UiCanvas.Width - W) / 2;

    /// <param name="unroll">0 = rolled up, 1 = fully open (the writing fades in at the end).</param>
    /// <summary>The list's buttons once it is written out: 0 starts the shift, 1 goes back, 2 turns the tutorial on or off.</summary>
    public UiHits Hits { get; } = new();

    /// <summary>The button under the mouse (0, 1 or 2), drawn darker.</summary>
    public int Hovered { get; set; } = -1;

    /// <summary>Whether round 1 will be the tutorial (Settings > Game > Tutorial helpers), shown on its button.</summary>
    public bool TutorialOn { get; set; } = true;

    public void Draw(UiCanvas ui, NaughtyNiceList list, float unroll, IInputService input)
    {
        Hits.Clear();
        ui.Fill(0, 0, UiCanvas.Width, UiCanvas.Height, Palette.WithAlpha(Palette.Ink, 165));

        float eased = 1 - MathF.Pow(1 - Math.Clamp(unroll, 0, 1), 3);
        float h = Math.Max(24, FullH * eased);
        float y = (UiCanvas.Height - h) / 2;

        DrawPaper(ui, X, y, W, h);
        float writing = Math.Clamp((unroll - 0.8f) / 0.2f, 0, 1);
        if (writing > 0) DrawWriting(ui, list, X, y, writing, input);
        DrawRod(ui, X, y - 12);
        DrawRod(ui, X, y + h - 10);
    }

    private static void DrawPaper(UiCanvas ui, float x, float y, float w, float h)
    {
        // Layers from the darker, browned edge to a paler middle, as old paper ages from the outside in.
        ui.FillRounded(x - 3, y - 2, w + 6, h + 4, ParchmentEdge, 0.04f);
        ui.FillRounded(x, y, w, h, Parchment, 0.03f);
        if (h > 30) ui.FillRounded(x + 10, y + 6, w - 20, h - 12, Palette.WithAlpha(ParchmentLight, 200), 0.05f);
        if (h > 60) ui.FillRounded(x + 26, y + 18, w - 52, h - 36, Palette.WithAlpha(ParchmentLight, 230), 0.07f);
        if (h > 120) ui.FillRounded(x + 60, y + 46, w - 120, h - 92, Palette.WithAlpha(ParchmentCentre, 170), 0.12f);

        // Uneven, scorched side edges (never a regular pattern): blotches sitting on the edge line.
        for (float fy = y + 4; fy < y + h - 4; fy += 6)
        {
            float k1 = Hash(fy), k2 = Hash(fy + 31.7f);
            if (k1 > 0.35f) ui.Circle(x + 1, fy, 2 + k1 * 6, Palette.WithAlpha(Scorch, (byte)(70 + k2 * 90)));
            if (k2 > 0.35f) ui.Circle(x + w - 1, fy + 3, 2 + k2 * 6, Palette.WithAlpha(Scorch, (byte)(70 + k1 * 90)));
        }

        // Tea-coloured stains, the same every time.
        foreach (var (sx, sy, r) in new[] { (0.16f, 0.74f, 36f), (0.84f, 0.28f, 28f), (0.64f, 0.88f, 22f), (0.3f, 0.18f, 15f), (0.9f, 0.7f, 12f) })
            if (sy * h > r && (1 - sy) * h > r)
            {
                ui.Circle(x + w * sx, y + h * sy, r, Stain);
                ui.Circle(x + w * sx + r * 0.3f, y + h * sy - r * 0.2f, r * 0.55f, Stain);
            }

        // A thin red and green ruled border inside the edge.
        if (h > 80)
        {
            ui.OutlineRounded(x + 18, y + 14, w - 36, h - 28, Palette.WithAlpha(Palette.Berry, 170), 2, 0.04f);
            ui.OutlineRounded(x + 23, y + 19, w - 46, h - 38, Palette.WithAlpha(Palette.Pine, 150), 1.2f, 0.04f);
        }
    }

    private static float Hash(float v) => MathF.Abs(MathF.Sin(v * 12.9898f) * 43758.5453f) % 1;

    /// <summary>A wooden rod with red-and-white candy-cane knobs.</summary>
    private static void DrawRod(UiCanvas ui, float x, float y)
    {
        ui.FillRounded(x - 22, y, W + 44, 22, Rod, 0.6f);
        ui.FillRounded(x - 18, y + 4, W + 36, 5, RodLight, 0.6f);
        foreach (float kx in new[] { x - 30, x + W + 30 })
        {
            ui.Circle(kx, y + 11, 15, Palette.Berry);
            ui.Line(kx - 11, y + 4, kx + 3, y + 22, 4, Palette.Snow);
            ui.Line(kx - 3, y, kx + 11, y + 18, 4, Palette.Snow);
            ui.Circle(kx - 5, y + 6, 3, Palette.WithAlpha(Palette.Snow, 170));
        }
    }

    private void DrawWriting(UiCanvas ui, NaughtyNiceList list, float x, float y, float alpha, IInputService input)
    {
        Color A(Color c) => Palette.WithAlpha(c, (byte)(c.A * alpha));

        Festive.HollySprig(ui, x + 40, y + 40, 1f, alpha);
        Festive.HollySprig(ui, x + W - 40, y + 40, -1f, alpha);

        // Title
        ui.Text("Santa's List", x + W / 2 + 2, y + 20, 38, A(Palette.Gold), Align.Center);
        ui.Text("Santa's List", x + W / 2, y + 18, 38, A(Palette.Berry), Align.Center);
        ui.Text("~ naughty & nice, checked twice ~", x + W / 2, y + 62, 14, A(InkBrown), Align.Center);
        ui.Line(x + 70, y + 86, x + W - 70, y + 86, 1.5f, A(ParchmentEdge));
        ui.Circle(x + W / 2, y + 86, 4, A(Palette.Berry));

        var nice = list.Entries.Where(e => !e.Naughty).ToList();
        var naughty = list.Entries.Where(e => e.Naughty).ToList();

        // Nice column, ticked off in green.
        float nx = x + 56, top = y + 100;
        ui.Text("NICE", nx, top, 20, A(Palette.Pine));
        float rowH = Math.Min(22, 250f / Math.Max(1, nice.Count));
        for (int i = 0; i < nice.Count; i++)
        {
            float ry = top + 28 + i * rowH;
            ui.Line(nx, ry + 8, nx + 5, ry + 13, 2.5f, A(Palette.Pine));
            ui.Line(nx + 5, ry + 13, nx + 13, ry + 2, 2.5f, A(Palette.Pine));
            ui.Text($"{nice[i].Child}, {nice[i].Age}", nx + 22, ry, 16, A(InkBrown));
            string toy = content.FindToy(nice[i].ToyId)?.Name ?? nice[i].ToyId;
            ui.Text(toy, nx + 200, ry + 2, 13, A(Palette.Shade(InkBrown, 1.6f)), Align.Right);
        }

        // Naughty column, in red, with coal.
        float bx = x + W / 2 + 30;
        ui.Text("NAUGHTY", bx, top, 20, A(Palette.Berry));
        if (naughty.Count == 0)
        {
            ui.Text("Everyone was nice", bx, top + 30, 15, A(InkBrown));
            ui.Text("this year!", bx, top + 50, 15, A(InkBrown));
        }
        for (int i = 0; i < naughty.Count; i++)
        {
            float ry = top + 30 + i * 34;
            DrawCoal(ui, bx + 8, ry + 9, A);
            ui.Text($"{naughty[i].Child}, {naughty[i].Age}", bx + 24, ry, 18, A(Palette.Shade(Palette.Berry, 0.8f)));
            string toy = content.FindToy(naughty[i].ToyId)?.Name ?? naughty[i].ToyId;
            ui.Text($"wanted a {toy.ToLowerInvariant()}", bx + 24, ry + 18, 12, A(Palette.Shade(InkBrown, 1.5f)));
        }

        if (naughty.Count > 0)
        {
            float ny = top + 40 + naughty.Count * 34;
            ui.Text("Naughty children get COAL.", bx, ny, 14, A(InkBrown));
            ui.Text("Grab it at the fireplace", bx, ny + 18, 14, A(InkBrown));
            ui.Text("and ship it like a toy.", bx, ny + 36, 14, A(InkBrown));

            // A slightly crooked rubber stamp.
            float sx = bx + 95, sy = ny + 100;
            for (int k = 0; k < 2; k++)
                ui.TextRotated("COAL!", sx + k, sy + k * 0.5f, 34, -12, A(Palette.WithAlpha(Palette.Berry, 200)));
        }

        // Santa's wax seal and the prompt.
        float wx = x + W - 58, wy = y + FullH - 64;
        ui.Circle(wx, wy, 21, A(Palette.Shade(Palette.Berry, 0.75f)));
        ui.Circle(wx, wy, 16, A(Palette.Berry));
        ui.Text("S", wx, wy - 11, 22, A(Palette.Gold), Align.Center);
        Button(0, $"{input.Label(InputAction.Confirm)}  Start the shift", x + 36, 184);
        Button(2, $"{input.Label(InputAction.ToggleMode)}  Tutorial: {(TutorialOn ? "On" : "Off")}", x + 228, 138);
        Button(1, $"{input.Label(InputAction.Back)}  Back", x + 374, 80);

        // Ink-outlined buttons on the parchment; the one under the mouse is filled in.
        void Button(int id, string text, float bx, float bw)
        {
            float by = y + FullH - 56, bh = 28;
            bool hot = Hovered == id;
            ui.FillRounded(bx, by, bw, bh, A(Palette.WithAlpha(hot ? Palette.Berry : InkBrown, (byte)(hot ? 230 : 25))), 0.5f);
            ui.OutlineRounded(bx, by, bw, bh, A(Palette.WithAlpha(InkBrown, 150)), 1.5f, 0.5f);
            ui.Text(ui.Fit(text, 14, bw - 10), bx + bw / 2, by + 6, 14, A(hot ? Palette.Snow : InkBrown), Align.Center);
            if (alpha > 0.5f) Hits.Add(id, bx, by, bw, bh);
        }
    }

    private static void DrawCoal(UiCanvas ui, float cx, float cy, Func<Color, Color> a)
    {
        ui.Circle(cx - 4, cy + 2, 6, a(Coal));
        ui.Circle(cx + 4, cy + 3, 5, a(Coal));
        ui.Circle(cx, cy - 3, 5, a(Coal));
        ui.Circle(cx - 1, cy - 4, 1.5f, a(Palette.WithAlpha(Palette.Snow, 90)));
    }
}
