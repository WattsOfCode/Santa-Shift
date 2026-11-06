using System.Numerics;
using Raylib_cs;

namespace SantaShift.Game.Views.UI;

public enum Align { Left, Center, Right }

/// <summary>
/// Draws 2D interface in logical 960 x 540 coordinates and scales it to the real window,
/// so text stays sharp at any window size. All UI views draw through this.
/// </summary>
public sealed class UiCanvas
{
    public const float Width = 960, Height = 540;

    public float Scale { get; }
    public Vector2 Offset { get; }
    public Font Font { get; }

    public UiCanvas(int screenWidth, int screenHeight, Font font)
    {
        Scale = MathF.Min(screenWidth / Width, screenHeight / Height);
        Offset = new Vector2((screenWidth - Width * Scale) / 2, (screenHeight - Height * Scale) / 2);
        Font = font;
    }

    /// <summary>The area of the real window the game picture occupies (letterboxed).</summary>
    public Rectangle ScreenArea => new(Offset.X, Offset.Y, Width * Scale, Height * Scale);

    public Rectangle ToScreen(float x, float y, float w, float h) =>
        new(Offset.X + x * Scale, Offset.Y + y * Scale, w * Scale, h * Scale);

    public void Fill(float x, float y, float w, float h, Color c) => Raylib.DrawRectangleRec(ToScreen(x, y, w, h), c);

    public void FillRounded(float x, float y, float w, float h, Color c, float roundness = 0.25f) =>
        Raylib.DrawRectangleRounded(ToScreen(x, y, w, h), roundness, 6, c);

    public void OutlineRounded(float x, float y, float w, float h, Color c, float thickness = 2, float roundness = 0.25f) =>
        Raylib.DrawRectangleRoundedLinesEx(ToScreen(x, y, w, h), roundness, 6, thickness * Scale, c);

    public void Circle(float x, float y, float r, Color c) =>
        Raylib.DrawCircleV(new Vector2(Offset.X + x * Scale, Offset.Y + y * Scale), r * Scale, c);

    public void Line(float x1, float y1, float x2, float y2, float thickness, Color c) =>
        Raylib.DrawLineEx(new Vector2(Offset.X + x1 * Scale, Offset.Y + y1 * Scale),
                          new Vector2(Offset.X + x2 * Scale, Offset.Y + y2 * Scale), thickness * Scale, c);

    public float MeasureText(string text, float size) => Raylib.MeasureTextEx(Font, text, size, Spacing(size)).X;

    /// <summary>Draws text with its top edge at y. Align sets which edge x refers to.</summary>
    public void Text(string text, float x, float y, float size, Color c, Align align = Align.Left)
    {
        float w = MeasureText(text, size);
        float left = align switch { Align.Center => x - w / 2, Align.Right => x - w, _ => x };
        Raylib.DrawTextEx(Font, text, new Vector2(Offset.X + left * Scale, Offset.Y + y * Scale), size * Scale, Spacing(size) * Scale, c);
    }

    /// <summary>Shortens text with "..." so it fits a width.</summary>
    public string Fit(string text, float size, float maxWidth)
    {
        if (MeasureText(text, size) <= maxWidth) return text;
        while (text.Length > 1 && MeasureText(text + "...", size) > maxWidth) text = text[..^1];
        return text + "...";
    }

    private static float Spacing(float size) => MathF.Max(1, size / 10);
}
