using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Game.Views.UI;

/// <summary>One order card: child, toy icon and name, requirement badges and time left (GDD section 9).</summary>
public sealed class OrderCardView(GameContent content)
{
    /// <summary>The widest a full card gets; the top bar may make them narrower (down to about 190).</summary>
    public const float Width = 248, Height = 72;

    /// <summary>The widest a compact card gets (they shrink to fit ten on the top bar).</summary>
    public const float CompactMaxWidth = 110;

    public void Draw(UiCanvas ui, Order order, long tick, float x, float y, float width = Width)
    {
        var t = content.Tuning;
        var toy = content.FindToy(order.ToyId);
        long remaining = order.RemainingTicks(tick);
        double seconds = (double)remaining / t.TicksPerSecond;
        bool urgent = seconds <= 10;
        float Width = width; // the card's own width from here on

        ui.FillRounded(x, y, Width, Height, order.Naughty ? Palette.Hex("#EBDDBF") : Palette.Snow, 0.18f);
        if (order.IsRush || order.Naughty) ui.OutlineRounded(x, y, Width, Height, Palette.Berry, 3, 0.18f);

        // Icon: a naughty child gets coal, whatever they asked for.
        ui.FillRounded(x + 6, y + 6, 52, 52, Palette.WithAlpha(Palette.Ink, 25), 0.2f);
        if (order.Naughty) ToyIcon.Draw(ui, Item.CoalId, x + 32, y + 32, 40, Palette.Ink);
        else ToyIcon.Draw(ui, order.ToyId, x + 32, y + 32, 40, toy != null ? Palette.Hex(toy.Color) : Palette.Wood);

        // Text: what to ship large, child small
        // A variant names itself ("Big-Wheel Train") in pine green, so it stands out from a plain toy.
        // On a narrow card a long name ("Big-Wheel Train") steps down a size or two before it is cut short.
        string what = order.Naughty ? "Coal" : toy?.NameOf(order.Variant) ?? order.ToyId;
        float nameRoom = Width - 66 - 8, nameSize = 20;
        while (nameSize > 15 && ui.MeasureText(what, nameSize) > nameRoom) nameSize--;
        ui.Text(ui.Fit(what, nameSize, nameRoom), x + 66, y + 8 + (20 - nameSize) / 2, nameSize,
            order.Variant != null && !order.Naughty ? Palette.Pine : Palette.Ink);
        string who = order.Letter != null ? $"for {order.Letter.Child}, {order.Letter.Age}" : $"Order #{order.Id}";
        ui.Text(ui.Fit(who, 14, Width - 66 - 46), x + 66, y + 32, 14, Palette.Shade(Palette.Ink, 1.6f));

        // Badges (a ribbon badge drops the word RIBBON when the card is too narrow for it)
        float bx = x + 66, right = x + Width - 6;
        if (order.Naughty) bx = Badge(ui, "NAUGHTY", bx, y + 50, Palette.Ink, Palette.Snow);
        if (order.RequiresRibbon)
        {
            // A coloured ribbon order names its colour, in that colour.
            var ribbon = t.FindRibbon(order.RibbonColor);
            var bg = ribbon != null ? Palette.Hex(ribbon.Color) : Palette.Gold;
            bool dark = bg.R * 0.3 + bg.G * 0.59 + bg.B * 0.11 < 140;
            string label = ribbon != null ? $"{ribbon.Name.ToUpperInvariant()} RIBBON" : "RIBBON";
            float rush = order.IsRush ? BadgeWidth(ui, "RUSH") + 4 : 0;
            if (ribbon != null && bx + BadgeWidth(ui, label) + rush > right) label = ribbon.Name.ToUpperInvariant();
            bx = Badge(ui, label, bx, y + 50, bg, dark ? Palette.Snow : Palette.Ink);
        }
        if (order.IsRush) Badge(ui, "RUSH", bx, y + 50, Palette.Berry, Palette.Snow);

        // Time left, beside the child's name
        string time = $"{(int)Math.Ceiling(seconds)}s";
        ui.Text(time, x + Width - 10, y + 29, 18, urgent ? Palette.Berry : Palette.Ink, Align.Right);

        // Draining bar along the bottom
        float frac = order.AllowedDurationTicks > 0 ? (float)remaining / order.AllowedDurationTicks : 0;
        ui.Fill(x + 8, y + Height - 6, Width - 16, 3, Palette.WithAlpha(Palette.Ink, 40));
        ui.Fill(x + 8, y + Height - 6, (Width - 16) * frac, 3, urgent ? Palette.Berry : Palette.Pine);
    }

    /// <summary>
    /// A small card for a busy board: the toy (or coal) and the seconds left, a ribbon tab in the wanted color, and a
    /// RUSH tag. Same colors and outline rules as the full card.
    /// </summary>
    public void DrawCompact(UiCanvas ui, Order order, long tick, float x, float y, float w)
    {
        var t = content.Tuning;
        var toy = content.FindToy(order.ToyId);
        long remaining = order.RemainingTicks(tick);
        double seconds = (double)remaining / t.TicksPerSecond;
        bool urgent = seconds <= 10;
        float h = Height;

        ui.FillRounded(x, y, w, h, order.Naughty ? Palette.Hex("#EBDDBF") : Palette.Snow, 0.2f);
        if (order.IsRush || order.Naughty) ui.OutlineRounded(x, y, w, h, Palette.Berry, 3, 0.2f);

        float cx = x + w / 2;
        if (order.Naughty) ToyIcon.Draw(ui, Item.CoalId, cx, y + 27, 30, Palette.Ink);
        else ToyIcon.Draw(ui, order.ToyId, cx, y + 27, 30, toy != null ? Palette.Hex(toy.Color) : Palette.Wood);

        if (order.IsRush)
        {
            float rw = ui.MeasureText("RUSH", 10) + 6;
            ui.FillRounded(x + 4, y + 4, rw, 13, Palette.Berry, 0.5f);
            ui.Text("RUSH", x + 7, y + 5, 10, Palette.Snow);
        }
        // A variant gets its short tag ("BIG", "ROCKET") in pine green under the icon, and the time moves down a little.
        bool tagged = false;
        if (!order.Naughty && order.Variant != null && toy?.Variant(order.Variant) is { } variant)
        {
            string tag = ui.Fit(variant.Tag.Length > 0 ? variant.Tag : variant.Name.ToUpperInvariant(), 10, w - 14);
            float tw = ui.MeasureText(tag, 10) + 6;
            ui.FillRounded(cx - tw / 2, y + 35, tw, 13, Palette.Pine, 0.5f);
            ui.Text(tag, cx, y + 36, 10, Palette.Snow, Align.Center);
            tagged = true;
        }
        if (order.RequiresRibbon)
        {
            var ribbon = t.FindRibbon(order.RibbonColor);
            var bg = ribbon != null ? Palette.Hex(ribbon.Color) : Palette.Gold;
            ui.FillRounded(x + w - 20, y + 4, 16, 13, bg, 0.5f);
            ui.OutlineRounded(x + w - 20, y + 4, 16, 13, Palette.WithAlpha(Palette.Ink, 90), 1, 0.5f);
        }

        ui.Text($"{(int)Math.Ceiling(seconds)}s", cx, y + (tagged ? 50 : 46), 16, urgent ? Palette.Berry : Palette.Ink, Align.Center);

        float frac = order.AllowedDurationTicks > 0 ? (float)remaining / order.AllowedDurationTicks : 0;
        ui.Fill(x + 6, y + h - 6, w - 12, 3, Palette.WithAlpha(Palette.Ink, 40));
        ui.Fill(x + 6, y + h - 6, (w - 12) * frac, 3, urgent ? Palette.Berry : Palette.Pine);
    }

    private static float BadgeWidth(UiCanvas ui, string text) => ui.MeasureText(text, 12) + 10;

    private static float Badge(UiCanvas ui, string text, float x, float y, Raylib_cs.Color bg, Raylib_cs.Color fg)
    {
        float w = BadgeWidth(ui, text);
        ui.FillRounded(x, y - 2, w, 16, bg, 0.5f);
        ui.Text(text, x + 5, y + 0, 12, fg);
        return x + w + 4;
    }
}
