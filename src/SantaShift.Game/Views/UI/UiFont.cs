using Raylib_cs;

namespace SantaShift.Game.Views.UI;

/// <summary>
/// The interface font. Raylib's built-in font is a 10-pixel bitmap, so scaled up to HUD sizes it turns blocky and soft;
/// a real outline font, loaded large with mipmaps, stays sharp at every size and window.
///
/// The game ships its own font, Barlow SemiBold (SIL Open Font License, Content/fonts/OFL.txt), so text looks the same
/// on Windows and macOS. Content/fonts/ui.ttf, if present, overrides it. If neither loads, a clear system font is used
/// in place (never shipped): Bahnschrift, Segoe UI Bold or Arial Bold on Windows; Arial Bold or Verdana Bold on macOS;
/// DejaVu Sans Bold on Linux. Last of all, Raylib's own font.
/// </summary>
public static class UiFont
{
    /// <summary>Glyphs are rasterised this big, then scaled down: crisp up to about 4K, smooth below via mipmaps.</summary>
    private const int BakeSize = 96;

    public static Font Load(string contentDirectory)
    {
        foreach (var path in Candidates(contentDirectory))
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
            var font = Raylib.LoadFontEx(path, BakeSize, null, 0); // the 95 printable ASCII characters (all the game uses)
            if (font.GlyphCount == 0 || font.Texture.Id == Raylib.GetFontDefault().Texture.Id) continue;
            Raylib.GenTextureMipmaps(ref font.Texture);
            Raylib.SetTextureFilter(font.Texture, TextureFilter.Trilinear);
            UiCanvas.LetterSpacing = 0.02f;
            IsCustom = true;
            return font;
        }
        UiCanvas.LetterSpacing = 0.1f; // the bitmap font needs a little air between letters
        return Raylib.GetFontDefault();
    }

    private static IEnumerable<string> Candidates(string contentDirectory)
    {
        string fonts = Path.Combine(contentDirectory, "fonts");
        yield return Path.Combine(fonts, "ui.ttf");
        yield return Path.Combine(fonts, "Barlow-SemiBold.ttf");

        if (OperatingSystem.IsWindows())
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            yield return Path.Combine(windows, "bahnschrift.ttf");
            yield return Path.Combine(windows, "segoeuib.ttf");
            yield return Path.Combine(windows, "arialbd.ttf");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/System/Library/Fonts/Supplemental/Arial Bold.ttf";
            yield return "/Library/Fonts/Arial Bold.ttf";
            yield return "/System/Library/Fonts/Supplemental/Verdana Bold.ttf";
        }
        else
        {
            yield return "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";
            yield return "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf";
        }
    }

    /// <summary>True when an outline font was loaded (it must be unloaded at exit).</summary>
    public static bool IsCustom { get; private set; }
}
