using System.Globalization;
using Raylib_cs;

namespace SantaShift.Game.Views;

/// <summary>The GDD's provisional palette (section 16), plus a few shades for grey-box shapes.</summary>
public static class Palette
{
    public static readonly Color Snow = Hex("#F4F0DC");
    public static readonly Color Ink = Hex("#202934");
    public static readonly Color Pine = Hex("#225E4C");
    public static readonly Color Berry = Hex("#B94146");
    public static readonly Color Gold = Hex("#E8BB57");
    public static readonly Color Wood = Hex("#8C6043");
    public static readonly Color Ice = Hex("#8EC9CF");

    public static readonly Color Floor = Hex("#C9A57E");
    public static readonly Color FloorStripe = Hex("#BC9670");
    public static readonly Color WoodDark = Hex("#6B4630");
    public static readonly Color Skin = Hex("#F2C9A0");
    public static readonly Color Shadow = new(0, 0, 0, 60);

    public static Color Hex(string hex)
    {
        string h = hex.TrimStart('#');
        if (h.Length != 6 || !uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            return new Color(255, 0, 255, 255); // obvious magenta for a typo in data
        return new Color((byte)(v >> 16), (byte)(v >> 8), (byte)v, (byte)255);
    }

    public static Color WithAlpha(Color c, byte a) => new(c.R, c.G, c.B, a);

    public static Color Shade(Color c, float factor) =>
        new((byte)Math.Clamp(c.R * factor, 0, 255), (byte)Math.Clamp(c.G * factor, 0, 255), (byte)Math.Clamp(c.B * factor, 0, 255), c.A);
}
