using SantaShift.Core.Rules;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.UI;

/// <summary>
/// The bench minigame panel: the time left, the parts on their number keys, and the order to hammer them on.
/// The order names the next part but never its key, so the player has to find it.
/// </summary>
public sealed class AssemblyPanelView
{
    public const float Width = 440, Height = 92;

    public void Draw(UiCanvas ui, Simulation sim, IInputService input, float x, float y)
    {
        if (sim.State.Assembly is not { } game) return;
        var toy = sim.Content.FindToy(game.ToyId);

        ui.FillRounded(x, y, Width, Height, Palette.WithAlpha(Palette.Ink, 225), 0.15f);
        ui.Text($"BUILD THE {(toy?.Name ?? game.ToyId).ToUpperInvariant()}", x + 12, y + 7, 14, Palette.Ice);

        // Time left
        float left = (float)game.TimeLeft(sim.State.Tick);
        const float barW = 170, barH = 10;
        float bx = x + Width - barW - 12, by = y + 10;
        ui.FillRounded(bx - 2, by - 2, barW + 4, barH + 4, Palette.Shade(Palette.Ink, 0.6f), 0.5f);
        if (left > 0) ui.FillRounded(bx, by, barW * left, barH, left < 0.3f ? Palette.Berry : Palette.Gold, 0.5f);
        double seconds = (double)(game.DeadlineTick - sim.State.Tick) / sim.Tuning.TicksPerSecond;
        ui.Text($"{Math.Max(0, seconds):0.0}s", bx - 8, y + 7, 14, left < 0.3f ? Palette.Berry : Palette.Snow, Align.Right);

        // The parts on their keys
        var placed = game.Order.Take(game.Placed).ToHashSet();
        int n = game.Keys.Count;
        const float gap = 8;
        float cardW = (Width - 24 - gap * (n - 1)) / n, cardH = 32, cy = y + 27;
        for (int i = 0; i < n; i++)
        {
            string part = game.Keys[i];
            bool done = placed.Contains(part);
            float cx = x + 12 + i * (cardW + gap);
            ui.FillRounded(cx, cy, cardW, cardH, done ? Palette.WithAlpha(Palette.Pine, 160) : Palette.Snow, 0.25f);
            // The key: big and gold on a dark chip, so it reads at a glance mid-build.
            string key = input.Label(InputAction.Part1 + i);
            float kw = Math.Max(26, ui.MeasureText(key, 20) + 12);
            ui.FillRounded(cx + 3, cy + 3, kw, cardH - 6, done ? Palette.WithAlpha(Palette.Ink, 120) : Palette.Ink, 0.3f);
            ui.Text(key, cx + 3 + kw / 2, cy + 5, 20, done ? Palette.Shade(Palette.Snow, 0.7f) : Palette.Gold, Align.Center);
            string name = sim.PartName(game.ToyId, part);
            ui.Text(ui.Fit(name, 17, cardW - kw - 14), cx + kw + 10, cy + 7, 17, done ? Palette.Shade(Palette.Snow, 0.8f) : Palette.Ink);
        }

        // The order
        float ox = x + 12, oy = y + 68;
        ui.Text("ORDER", ox, oy, 12, Palette.Ice);
        ox += ui.MeasureText("ORDER", 12) + 10;
        for (int i = 0; i < game.Order.Count; i++)
        {
            string name = sim.PartName(game.ToyId, game.Order[i]);
            bool done = i < game.Placed, next = i == game.Placed;
            var colour = done ? Palette.Gold : next ? Palette.Snow : Palette.Shade(Palette.Snow, 0.55f);
            float w = ui.MeasureText(name, 16);
            if (next) ui.FillRounded(ox - 5, oy - 3, w + 10, 22, Palette.WithAlpha(Palette.Berry, 200), 0.4f);
            ui.Text(name, ox, oy, 16, colour);
            ox += w + 8;
            if (i < game.Order.Count - 1)
            {
                ui.Text(">", ox, oy, 16, Palette.Shade(Palette.Snow, 0.55f));
                ox += ui.MeasureText(">", 16) + 8;
            }
        }
    }
}
