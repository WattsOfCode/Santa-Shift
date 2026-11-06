using SantaShift.Core.Rules;

namespace SantaShift.Game.Views.UI;

/// <summary>Santa's portrait strip under the order cards: announcements, the current policy, or the newest letter.</summary>
public sealed class SantaPanelView
{
    public void Draw(UiCanvas ui, Simulation sim, Feedback feedback, float x, float y, float w, float h)
    {
        ui.FillRounded(x, y, w, h, Palette.WithAlpha(Palette.Ink, 200), 0.3f);
        DrawPortrait(ui, x + 16, y + h / 2, h / 2 - 2, talking: feedback.SantaTicks > 0 && (feedback.SantaTicks / 8) % 2 == 0);

        float tx = x + 38;
        if (feedback.SantaLine != null)
        {
            ui.Text(ui.Fit($"\"{feedback.SantaLine}\"", 14, w * 0.55f), tx, y + 7, 14, Palette.Snow);
            if (feedback.SantaText != null)
                ui.Text(ui.Fit(feedback.SantaText, 14, w * 0.4f), x + w - 10, y + 7, 14, Palette.Gold, Align.Right);
            return;
        }

        string policy = CurrentPolicy(sim);
        if (policy.Length > 0) { ui.Text(policy, tx, y + 7, 14, Palette.Gold); return; }
        if (feedback.LetterText != null) ui.Text(ui.Fit(feedback.LetterText, 14, w - 50), tx, y + 7, 14, Palette.Snow);
    }

    /// <summary>A short, always-visible summary of rules currently in force.</summary>
    public static string CurrentPolicy(Simulation sim)
    {
        var e = sim.State.Events;
        var parts = new List<string>();
        if (e.RibbonActive(sim.State.Tick))
        {
            int secs = (int)Math.Ceiling((double)(e.RibbonWindowEndTick - sim.State.Tick) / sim.Tuning.TicksPerSecond);
            parts.Add($"POLICY: new orders need ribbons ({secs}s)");
        }
        if (e.PendingRush) parts.Add("POLICY: next new order is RUSH");
        return string.Join("   ", parts);
    }

    private static void DrawPortrait(UiCanvas ui, float cx, float cy, float r, bool talking)
    {
        ui.Circle(cx, cy, r, Palette.Berry);                  // hat / coat
        ui.Circle(cx, cy + r * 0.15f, r * 0.7f, Palette.Skin); // face
        ui.Circle(cx, cy + r * 0.6f, r * 0.55f, Palette.Snow); // beard
        ui.Fill(cx - r * 0.25f, cy + r * 0.35f, r * 0.5f, talking ? r * 0.25f : r * 0.08f, Palette.Ink); // mouth
    }
}
