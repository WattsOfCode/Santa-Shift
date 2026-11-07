using System.Numerics;
using Raylib_cs;

namespace SantaShift.Game.Views.UI;

/// <summary>
/// Christmas decorations drawn from simple shapes in UI coordinates: holly, candy canes, glass baubles and falling
/// snow. Shared by the title screen and Santa's list so they look the same everywhere. No textures, so they cost nothing.
/// </summary>
public static class Festive
{
    public static readonly Color Holly = Palette.Hex("#1F6B3E");
    public static readonly Color HollyDark = Palette.Hex("#124A2A");
    public static readonly Color HollyLight = Palette.Hex("#2F8A52");
    public static readonly Color CaneRed = Palette.Hex("#C8323A");
    public static readonly Color CaneWhite = Palette.Hex("#FBF6EC");

    private static Color Fade(Color c, float alpha) => Palette.WithAlpha(c, (byte)(c.A * Math.Clamp(alpha, 0, 1)));

    /// <summary>One spiky holly leaf from <paramref name="root"/> along <paramref name="dir"/> (a unit vector).</summary>
    public static void HollyLeaf(UiCanvas ui, Vector2 root, Vector2 dir, float len, float wid, Color leaf, Color vein)
    {
        var side = new Vector2(-dir.Y, dir.X);
        // Three points down each side: a diamond with notches cut in.
        for (int s = 0; s < 3; s++)
        {
            float t0 = s / 3f, t1 = (s + 1) / 3f;
            var a = root + dir * (len * t0);
            var b = root + dir * (len * t1);
            float w0 = wid * MathF.Sin(MathF.PI * (t0 + 0.12f)), w1 = wid * MathF.Sin(MathF.PI * Math.Min(1, t1 + 0.02f));
            var mid = root + dir * (len * (t0 + t1) / 2);
            ui.Triangle(a + side * w0 * 0.6f, b + side * w1 * 0.6f, mid + side * (wid * 1.15f), leaf);
            ui.Triangle(a - side * w0 * 0.6f, b - side * w1 * 0.6f, mid - side * (wid * 1.15f), leaf);
            ui.Triangle(a + side * w0 * 0.6f, b + side * w1 * 0.6f, b - side * w1 * 0.6f, leaf);
            ui.Triangle(a + side * w0 * 0.6f, a - side * w0 * 0.6f, b - side * w1 * 0.6f, leaf);
        }
        ui.Line(root.X, root.Y, root.X + dir.X * len, root.Y + dir.Y * len, Math.Max(1f, wid * 0.14f), vein);
    }

    /// <summary>A shiny red holly berry.</summary>
    public static void Berry(UiCanvas ui, float x, float y, float r, float alpha = 1f)
    {
        ui.Circle(x, y, r, Fade(Palette.Shade(Palette.Berry, 0.8f), alpha));
        ui.Circle(x - r * 0.12f, y - r * 0.12f, r * 0.82f, Fade(Palette.Berry, alpha));
        ui.Circle(x - r * 0.3f, y - r * 0.3f, r * 0.3f, Fade(Palette.WithAlpha(Palette.Snow, 210), alpha));
    }

    /// <summary>
    /// A sprig: three leaves fanned out and three berries. <paramref name="flip"/> mirrors it (-1), <paramref name="turn"/>
    /// rotates it (radians) and <paramref name="scale"/> sizes it (1 = 30 px leaves).
    /// </summary>
    public static void HollySprig(UiCanvas ui, float cx, float cy, float flip = 1f, float alpha = 1f, float scale = 1f, float turn = 0f)
    {
        var root = new Vector2(cx, cy);
        foreach (float angle in new[] { -0.5f, 0.45f, 1.35f })
        {
            float a = angle * flip + (flip < 0 ? MathF.PI : 0) + turn;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            HollyLeaf(ui, root, dir, 30 * scale, 9 * scale, Fade(Holly, alpha), Fade(HollyDark, alpha));
        }
        foreach (var (bx, by) in new[] { (0f, 0f), (7f, 5f), (-2f, 8f) })
        {
            var o = Rotate(new Vector2(bx * flip, by), turn) * scale;
            Berry(ui, cx + o.X, cy + o.Y, 5.5f * scale, alpha);
        }
    }

    /// <summary>
    /// A red-and-white candy cane standing on <paramref name="bottomY"/> at x, its hook curling over at
    /// <paramref name="topY"/> towards <paramref name="hookSide"/> (-1 left, +1 right), wrapped in a holly vine.
    /// </summary>
    public static void CandyCane(UiCanvas ui, float x, float topY, float bottomY, float hookSide, float thickness = 15f, float sway = 0f)
    {
        float r = thickness * 1.25f, h = thickness / 2;
        float shaft = bottomY - topY, arc = MathF.PI * r, tail = r * 0.55f, total = shaft + arc + tail;

        Vector2 P(float s)
        {
            s = Math.Clamp(s, 0, total);
            Vector2 p;
            if (s <= shaft) p = new Vector2(x, bottomY - s);
            else if (s <= shaft + arc)
            {
                float t = (s - shaft) / r;
                p = new Vector2(x + hookSide * r - hookSide * r * MathF.Cos(t), topY - r * MathF.Sin(t));
            }
            else p = new Vector2(x + 2 * hookSide * r, topY + (s - shaft - arc));
            // A gentle lean from the bottom (the cane rocks a little).
            return Rotate(p - new Vector2(x, bottomY), sway) + new Vector2(x, bottomY);
        }
        Vector2 N(float s)
        {
            var d = P(s + 0.5f) - P(s - 0.5f);
            return Vector2.Normalize(new Vector2(-d.Y, d.X));
        }

        // Shadow, then the white cane as a chain of round-ended segments.
        const float step = 4.5f;
        for (float s = 0; s < total; s += step)
        {
            var a = P(s) + new Vector2(3, 4);
            var b = P(s + step) + new Vector2(3, 4);
            ui.Line(a.X, a.Y, b.X, b.Y, thickness, Palette.WithAlpha(Palette.Ink, 90));
        }
        for (float s = 0; s < total; s += step)
        {
            var a = P(s);
            var b = P(s + step);
            ui.Line(a.X, a.Y, b.X, b.Y, thickness, CaneWhite);
            ui.Circle(a.X, a.Y, h, CaneWhite);
        }
        var end = P(total);
        ui.Circle(end.X, end.Y, h, CaneWhite);

        // Slanted red stripes: a parallelogram across the cane every few pixels along it.
        float period = thickness * 1.3f, slant = thickness * 0.7f;
        for (float s0 = 0; s0 + period / 2 + slant <= total; s0 += period)
        {
            float s1 = s0 + period / 2;
            var a = P(s0) + N(s0) * h; var b = P(s1) + N(s1) * h;
            var c = P(s1 + slant) - N(s1 + slant) * h; var d = P(s0 + slant) - N(s0 + slant) * h;
            ui.Triangle(a, b, c, CaneRed);
            ui.Triangle(a, c, d, CaneRed);
        }
        // Shine down the side facing the light.
        for (float s = 4; s < shaft + arc * 0.5f; s += step)
        {
            var a = P(s) - N(s) * h * 0.45f;
            var b = P(s + step) - N(s + step) * h * 0.45f;
            ui.Line(a.X, a.Y, b.X, b.Y, thickness * 0.16f, Palette.WithAlpha(Color.White, 130));
        }

        WrapInHolly(ui, P, N, shaft, h, hookSide);
    }

    /// <summary>A holly vine spiralling up the cane's shaft, with leaves and berries, and a sprig where the hook starts.</summary>
    private static void WrapInHolly(UiCanvas ui, Func<float, Vector2> p, Func<float, Vector2> n, float shaft, float h, float hookSide)
    {
        const float turns = 2.2f;
        float start = h * 1.5f, end = shaft - h * 0.5f;
        Vector2 Vine(float s, out bool front)
        {
            float phase = (s - start) / (end - start) * turns * MathF.Tau;
            front = MathF.Cos(phase) > 0;
            return p(s) + n(s) * (MathF.Sin(phase) * h * 1.15f);
        }
        for (float s = start; s < end; s += 2f)
        {
            var a = Vine(s, out bool front);
            var b = Vine(s + 2f, out _);
            ui.Line(a.X, a.Y, b.X, b.Y, front ? 2.6f : 1.8f, front ? HollyDark : Palette.WithAlpha(HollyDark, 140));
        }
        // Leaves sprouting from the vine on alternate sides, pointing out and up.
        int k = 0;
        for (float s = start + 10; s < end - 4; s += 24, k++)
        {
            var at = Vine(s, out _);
            float side = k % 2 == 0 ? -1 : 1;
            var dir = Vector2.Normalize(n(s) * side * 1.0f + new Vector2(0, -0.55f));
            HollyLeaf(ui, at, dir, 19, 6, k % 3 == 0 ? HollyLight : Holly, HollyDark);
            if (k % 2 == 1) Berry(ui, at.X, at.Y, 3.6f);
        }
        // A bushy sprig where the hook begins, with a berry cluster.
        var top = p(shaft);
        Festive.HollySprig(ui, top.X - hookSide * 2, top.Y + 6, -hookSide, 1f, 0.85f, 0.35f * hookSide);
    }

    /// <summary>
    /// A glass bauble hanging on a thread from the top edge. <paramref name="swing"/> tilts the thread (radians).
    /// Styles: 0 plain, 1 gold band, 2 dots, 3 zigzag.
    /// </summary>
    public static void Ornament(UiCanvas ui, float anchorX, float threadLength, float radius, Color color, int style, float swing)
    {
        var top = new Vector2(anchorX, 0);
        var dir = new Vector2(MathF.Sin(swing), MathF.Cos(swing));
        var cap = top + dir * threadLength;
        ui.Line(top.X, top.Y, cap.X, cap.Y, 1.2f, Palette.WithAlpha(Palette.Gold, 200));
        var c = cap + dir * (radius + 5);

        ui.Circle(c.X + 3, c.Y + 4, radius, Palette.WithAlpha(Palette.Ink, 80));            // shadow
        ui.Circle(c.X, c.Y, radius, Palette.Shade(color, 0.7f));
        ui.Circle(c.X - radius * 0.08f, c.Y - radius * 0.1f, radius * 0.9f, color);
        switch (style)
        {
            case 1:
                ui.Fill(c.X - radius * 0.97f, c.Y - radius * 0.14f, radius * 1.94f, radius * 0.28f, Palette.Gold);
                ui.Fill(c.X - radius * 0.97f, c.Y - radius * 0.05f, radius * 1.94f, radius * 0.06f, Palette.Shade(Palette.Gold, 0.75f));
                break;
            case 2:
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * MathF.Tau + 0.3f;
                    ui.Circle(c.X + MathF.Cos(a) * radius * 0.55f, c.Y + MathF.Sin(a) * radius * 0.55f, radius * 0.11f, Palette.Snow);
                }
                break;
            case 3:
                for (int i = 0; i < 6; i++)
                {
                    float x0 = c.X - radius * 0.8f + i * radius * 0.32f;
                    float y0 = c.Y + (i % 2 == 0 ? -1 : 1) * radius * 0.13f;
                    float y1 = c.Y + (i % 2 == 0 ? 1 : -1) * radius * 0.13f;
                    ui.Line(x0, y0, x0 + radius * 0.32f, y1, radius * 0.1f, Palette.Snow);
                }
                break;
        }
        ui.Circle(c.X - radius * 0.38f, c.Y - radius * 0.4f, radius * 0.22f, Palette.WithAlpha(Color.White, 170)); // shine
        ui.Circle(c.X - radius * 0.18f, c.Y - radius * 0.55f, radius * 0.08f, Palette.WithAlpha(Color.White, 200));

        // Gold cap and its wire loop, turned with the thread.
        var capCentre = c - dir * radius;
        var side = new Vector2(dir.Y, -dir.X) * radius * 0.3f;
        var capTop = capCentre - dir * 6;
        ui.Triangle(capCentre + side, capCentre - side, capTop - side, Palette.Shade(Palette.Gold, 0.85f));
        ui.Triangle(capCentre + side, capTop - side, capTop + side, Palette.Shade(Palette.Gold, 0.85f));
        ui.Line(capTop.X - side.X * 0.9f, capTop.Y - side.Y * 0.9f, capTop.X + side.X * 0.9f, capTop.Y + side.Y * 0.9f, 1.5f, Palette.Gold);
        ui.Circle(cap.X, cap.Y, 2.6f, Palette.Gold);
        ui.Circle(cap.X, cap.Y, 1.3f, Palette.Ink);
    }

    /// <summary>Soft snow drifting down the screen. Still when <paramref name="moving"/> is false (reduced motion).</summary>
    public static void Snowfall(UiCanvas ui, double time, int flakes = 70, bool moving = true)
    {
        float t = moving ? (float)time : 0f;
        for (int i = 0; i < flakes; i++)
        {
            float speed = 14 + Hash(i * 3.1f) * 26;
            float x = Hash(i * 1.7f) * UiCanvas.Width + MathF.Sin(t * (0.6f + Hash(i) * 0.8f) + i) * 10;
            float y = (Hash(i * 5.3f) * (UiCanvas.Height + 20) + t * speed) % (UiCanvas.Height + 20) - 10;
            float r = 1f + Hash(i * 7.9f) * 1.8f;
            ui.Circle(x, y, r, Palette.WithAlpha(Palette.Snow, (byte)(90 + Hash(i * 2.2f) * 110)));
        }
    }

    private static float Hash(float v) => MathF.Abs(MathF.Sin(v * 12.9898f + 1.3f) * 43758.5453f) % 1;

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = MathF.Cos(radians), s = MathF.Sin(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}
