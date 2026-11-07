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
            case "coal": // a heap of black lumps with a glint
                ui.Circle(cx - s * 0.4f, cy + s * 0.25f, s * 0.42f, Palette.Hex("#26262B"));
                ui.Circle(cx + s * 0.42f, cy + s * 0.3f, s * 0.36f, Palette.Hex("#303036"));
                ui.Circle(cx + s * 0.02f, cy - s * 0.28f, s * 0.45f, Palette.Hex("#1C1C20"));
                ui.Circle(cx - s * 0.12f, cy - s * 0.45f, s * 0.1f, Palette.WithAlpha(Palette.Snow, 120));
                break;
            case "supply": // a dark crate with a green strap and a gold star
                ui.FillRounded(cx - s * 0.85f, cy - s * 0.7f, s * 1.7f, s * 1.4f, Palette.Hex("#6B4A2C"), 0.15f);
                ui.Fill(cx - s * 0.85f, cy - s * 0.7f, s * 1.7f, s * 0.22f, Palette.Hex("#4A3220"));
                ui.Fill(cx - s * 0.12f, cy - s * 0.7f, s * 0.24f, s * 1.4f, Palette.Pine);
                ui.Circle(cx + s * 0.45f, cy + s * 0.1f, s * 0.2f, Palette.Gold);
                break;
            default:
                ui.FillRounded(cx - s * 0.7f, cy - s * 0.7f, s * 1.4f, s * 1.4f, color);
                break;
        }
    }
}
