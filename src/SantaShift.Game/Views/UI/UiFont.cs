using Raylib_cs;

namespace SantaShift.Game.Views.UI;

/// <summary>
/// The interface font. Raylib's built-in font is a 10-pixel bitmap, so scaled up to HUD sizes it turns blocky and soft;
/// a real outline font, loaded large with mipmaps, stays sharp at every size and window.
///
/// Looks first for Content/fonts/ui.ttf (drop any font with a licence that allows shipping it there, e.g. an OFL one),
/// then for a clear Windows font that every Windows 10/11 PC has (Bahnschrift, then Segoe UI Bold, then Arial Bold;
/// used from the system, not shipped), and falls back to Raylib's own font if none loads.
/// </summary>
public static class UiFont
{
    /// <summary>Glyphs are rasterised this big, then scaled down: crisp up to about 4K, smooth below via mipmaps.</summary>
    private const int BakeSize = 96;

    public static Font Load(string contentDirectory)
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string[] candidates =
        [
            Path.Combine(contentDirectory, "fonts", "ui.ttf"),
            Path.Combine(windows, "bahnschrift.ttf"),
            Path.Combine(windows, "segoeuib.ttf"),
            Path.Combine(windows, "arialbd.ttf"),
        ];
        foreach (var path in candidates)
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

    /// <summary>True when an outline font was loaded (it must be unloaded at exit).</summary>
    public static bool IsCustom { get; private set; }
}
