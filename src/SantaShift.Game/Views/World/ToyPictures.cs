using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Views.World;

/// <summary>
/// The picture of each toy that is pasted on its kit crates and its bin's sign: the same silhouette as on the order
/// cards, on a snow-white card framed in the toy's colour. Painted once at startup into small textures, so there is no
/// image file to keep in step with toys.json; a new toy gets its picture automatically.
/// </summary>
public static class ToyPictures
{
    /// <summary>Texture size in pixels (a power of two, so it can have mipmaps and stay crisp from across the room).</summary>
    private const int Pixels = 128;

    public static void PaintAll(GameContent content, AssetLibrary assets)
    {
        var font = Raylib.GetFontDefault();
        foreach (var toy in content.Toys) assets.AddPicture(toy.Id, Paint(toy, font));
    }

    private static Texture2D Paint(Toy toy, Font font)
    {
        var colour = Palette.Hex(toy.Color);
        var target = Raylib.LoadRenderTexture(Pixels, Pixels);
        Raylib.BeginTextureMode(target);
        Raylib.ClearBackground(colour); // the frame, edge to edge (no see-through corners to sort)
        var ui = UiCanvas.ForSquare(Pixels, font);
        ui.FillRounded(7, 7, 86, 86, Palette.Snow, 0.18f);
        ToyIcon.Draw(ui, toy.Id, 50, 52, 78, colour);
        Raylib.EndTextureMode();

        // Render textures come out upside down; copy into a plain texture the right way up, with mipmaps.
        var image = Raylib.LoadImageFromTexture(target.Texture);
        Raylib.ImageFlipVertical(ref image);
        var texture = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        Raylib.UnloadRenderTexture(target);
        Raylib.GenTextureMipmaps(ref texture);
        Raylib.SetTextureFilter(texture, TextureFilter.Trilinear);
        return texture;
    }
}
