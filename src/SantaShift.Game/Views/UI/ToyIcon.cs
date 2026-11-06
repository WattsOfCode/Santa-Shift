using Raylib_cs;

namespace SantaShift.Game.Views.UI;

/// <summary>Flat toy silhouettes for order cards and the HUD. Shape, not just colour, tells toys apart (GDD section 21).</summary>
public static class ToyIcon
{
    public static void Draw(UiCanvas ui, string toyId, float cx, float cy, float size, Color color)
    {
        float s = size / 2;
        switch (toyId)
        {
            case "train": // boiler, cab, chimney and wheels
                ui.Fill(cx - s * 0.9f, cy - s * 0.3f, s * 1.1f, s * 0.6f, color);
                ui.Fill(cx + s * 0.2f, cy - s * 0.7f, s * 0.6f, s * 1.0f, Palette.Ink);
                ui.Fill(cx - s * 0.75f, cy - s * 0.7f, s * 0.22f, s * 0.4f, Palette.Ink);
                ui.Circle(cx - s * 0.55f, cy + s * 0.45f, s * 0.22f, Palette.Ink);
                ui.Circle(cx - s * 0.05f, cy + s * 0.45f, s * 0.22f, Palette.Ink);
                ui.Circle(cx + s * 0.5f, cy + s * 0.45f, s * 0.22f, Palette.Ink);
                break;
            case "bear": // round head, ears, contrasting face
                ui.Circle(cx - s * 0.55f, cy - s * 0.55f, s * 0.28f, color);
                ui.Circle(cx + s * 0.55f, cy - s * 0.55f, s * 0.28f, color);
                ui.Circle(cx, cy, s * 0.75f, color);
                ui.Circle(cx, cy + s * 0.2f, s * 0.3f, Palette.Snow);
                ui.Circle(cx - s * 0.25f, cy - s * 0.15f, s * 0.09f, Palette.Ink);
                ui.Circle(cx + s * 0.25f, cy - s * 0.15f, s * 0.09f, Palette.Ink);
                break;
            case "robot": // square head and antenna
                ui.Line(cx, cy - s * 0.95f, cx, cy - s * 0.55f, s * 0.12f, Palette.Ink);
                ui.Circle(cx, cy - s * 0.95f, s * 0.14f, Palette.Berry);
                ui.Fill(cx - s * 0.65f, cy - s * 0.55f, s * 1.3f, s * 1.3f, color);
                ui.Fill(cx - s * 0.4f, cy - s * 0.25f, s * 0.22f, s * 0.22f, Palette.Ink);
                ui.Fill(cx + s * 0.18f, cy - s * 0.25f, s * 0.22f, s * 0.22f, Palette.Ink);
                ui.Fill(cx - s * 0.35f, cy + s * 0.3f, s * 0.7f, s * 0.12f, Palette.Ink);
                break;
            default:
                ui.FillRounded(cx - s * 0.7f, cy - s * 0.7f, s * 1.4f, s * 1.4f, color);
                break;
        }
    }
}
