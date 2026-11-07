using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Game.Views.UI;

/// <summary>One order card: child, toy icon and name, requirement badges and time left (GDD section 9).</summary>
public sealed class OrderCardView(GameContent content)
{
    public const float Width = 248, Height = 72;

    public void Draw(UiCanvas ui, Order order, long tick, float x, float y)
    {
        var t = content.Tuning;
        var toy = content.FindToy(order.ToyId);
        long remaining = order.RemainingTicks(tick);
        double seconds = (double)remaining / t.TicksPerSecond;
        bool urgent = seconds <= 10;

        ui.FillRounded(x, y, Width, Height, order.Naughty ? Palette.Hex("#EBDDBF") : Palette.Snow, 0.18f);
        if (order.IsRush || order.Naughty) ui.OutlineRounded(x, y, Width, Height, Palette.Berry, 3, 0.18f);

        // Icon: a naughty child gets coal, whatever they asked for.
        ui.FillRounded(x + 6, y + 6, 52, 52, Palette.WithAlpha(Palette.Ink, 25), 0.2f);
        if (order.Naughty) ToyIcon.Draw(ui, Item.CoalId, x + 32, y + 32, 40, Palette.Ink);
        else ToyIcon.Draw(ui, order.ToyId, x + 32, y + 32, 40, toy != null ? Palette.Hex(toy.Color) : Palette.Wood);

        // Text: what to ship large, child small
        ui.Text(order.Naughty ? "Coal" : toy?.Name ?? order.ToyId, x + 66, y + 8, 20, Palette.Ink);
        string who = order.Letter != null ? $"for {order.Letter.Child}, {order.Letter.Age}" : $"Order #{order.Id}";
        ui.Text(ui.Fit(who, 14, 110), x + 66, y + 32, 14, Palette.Shade(Palette.Ink, 1.6f));

        // Badges
        float bx = x + 66;
        if (order.Naughty) bx = Badge(ui, "NAUGHTY", bx, y + 50, Palette.Ink, Palette.Snow);
        if (order.RequiresRibbon)
        {
            // A coloured ribbon order names its colour, in that colour.
            var ribbon = t.FindRibbon(order.RibbonColor);
            var bg = ribbon != null ? Palette.Hex(ribbon.Color) : Palette.Gold;
            bool dark = bg.R * 0.3 + bg.G * 0.59 + bg.B * 0.11 < 140;
            bx = Badge(ui, ribbon != null ? $"{ribbon.Name.ToUpperInvariant()} RIBBON" : "RIBBON", bx, y + 50, bg, dark ? Palette.Snow : Palette.Ink);
        }
        if (order.IsRush) Badge(ui, "RUSH", bx, y + 50, Palette.Berry, Palette.Snow);

        // Time left
        string time = $"{(int)Math.Ceiling(seconds)}s";
        ui.Text(time, x + Width - 10, y + 8, 22, urgent ? Palette.Berry : Palette.Ink, Align.Right);

        // Draining bar along the bottom
        float frac = order.AllowedDurationTicks > 0 ? (float)remaining / order.AllowedDurationTicks : 0;
        ui.Fill(x + 8, y + Height - 6, Width - 16, 3, Palette.WithAlpha(Palette.Ink, 40));
        ui.Fill(x + 8, y + Height - 6, (Width - 16) * frac, 3, urgent ? Palette.Berry : Palette.Pine);
    }

    private static float Badge(UiCanvas ui, string text, float x, float y, Raylib_cs.Color bg, Raylib_cs.Color fg)
    {
        float w = ui.MeasureText(text, 12) + 10;
        ui.FillRounded(x, y - 2, w, 16, bg, 0.5f);
        ui.Text(text, x + 5, y + 0, 12, fg);
        return x + w + 4;
    }
}
