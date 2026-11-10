using SantaShift.Core.Models;
using SantaShift.Core.Rules;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.UI;

/// <summary>
/// The bench minigame panel: the time left, the toy's parts on their keys, and its build combinations (the plain toy
/// and any variants this round), each as the keys to press in turn. The combination the most urgent letter wants is
/// marked. From a later round the combinations are no longer listed (only their names): the elf builds from memory.
/// </summary>
public sealed class AssemblyPanelView
{
    public const float Width = 440;
    private const float Top = 66, RecipeRow = 24;

    /// <summary>Draws the panel with its bottom edge at <paramref name="bottom"/>.</summary>
    public void Draw(UiCanvas ui, Simulation sim, IInputService input, float x, float bottom)
    {
        if (sim.State.Assembly is not { } game) return;
        var toy = sim.Content.FindToy(game.ToyId);
        var d = sim.Content.Dialogue;
        var a = sim.Tuning.Assembly;
        bool show = a.ShowRecipes(sim.State.Mode, sim.State.Round);
        float height = Top + game.Recipes.Count * RecipeRow + 4;
        float y = bottom - height;

        // In the round the bench changes (variants arrive, or the combinations stop being listed), a gold tag says so.
        if (a.PolicyChange(sim.State.Mode, sim.State.Round) is { } change)
        {
            string notice = d.Message(change == "assembly_memory" ? "assembly_policy_memory" : "assembly_policy_variants");
            float nw = ui.MeasureText(notice, 13) + 20;
            var tag = (sim.State.Tick / 20) % 2 == 0 ? Palette.Shade(Palette.Gold, 1.2f) : Palette.Gold;
            ui.OutlineRounded(x + (Width - nw) / 2 - 2, y - 26, nw + 4, 24, Palette.WithAlpha(Palette.Snow, 170), 1.5f, 0.5f);
            ui.FillRounded(x + (Width - nw) / 2, y - 24, nw, 20, tag, 0.5f);
            ui.Text(notice, x + Width / 2, y - 21, 13, Palette.Ink, Align.Center);
        }

        ui.FillRounded(x, y, Width, height, Palette.WithAlpha(Palette.Ink, 225), 0.15f);
        ui.Text($"BUILD THE {(toy?.Name ?? game.ToyId).ToUpperInvariant()}", x + 12, y + 7, 14, Palette.Ice);

        // Time left
        float left = (float)game.TimeLeft(sim.State.Tick);
        const float barW = 170, barH = 10;
        float bx = x + Width - barW - 12, by = y + 10;
        ui.FillRounded(bx - 2, by - 2, barW + 4, barH + 4, Palette.Shade(Palette.Ink, 0.6f), 0.5f);
        if (left > 0) ui.FillRounded(bx, by, barW * left, barH, left < 0.3f ? Palette.Berry : Palette.Gold, 0.5f);
        double seconds = (double)(game.DeadlineTick - sim.State.Tick) / sim.Tuning.TicksPerSecond;
        ui.Text($"{Math.Max(0, seconds):0.0}s", bx - 8, y + 7, 14, left < 0.3f ? Palette.Berry : Palette.Snow, Align.Right);

        // The parts on their keys (the same keys every build of this toy)
        var keyed = Enumerable.Range(0, game.Keys.Count).Where(i => game.Keys[i].Length > 0).ToList();
        int n = keyed.Count;
        const float gap = 8;
        float cardW = (Width - 24 - gap * (n - 1)) / Math.Max(1, n), cardH = 32, cy = y + 27;
        for (int c = 0; c < n; c++)
        {
            int i = keyed[c];
            string part = game.Keys[i];
            // A part that can go on (again) stays bright; one that is on and not needed again turns green.
            bool usable = game.Recipes.Any(r => game.Fits(r) && r.Order[game.Placed] == part);
            bool done = !usable && game.PlacedParts.Contains(part);
            float cx = x + 12 + c * (cardW + gap);
            var card = done ? Palette.WithAlpha(Palette.Pine, 160) : usable || !show ? Palette.Snow : Palette.Shade(Palette.Snow, 0.72f);
            ui.FillRounded(cx, cy, cardW, cardH, card, 0.25f);
            // The key: big and gold on a dark chip, so it reads at a glance mid-build.
            string key = input.Label(InputAction.Part1 + i);
            float kw = Math.Max(26, ui.MeasureText(key, 20) + 12);
            ui.FillRounded(cx + 3, cy + 3, kw, cardH - 6, done ? Palette.WithAlpha(Palette.Ink, 120) : Palette.Ink, 0.3f);
            ui.Text(key, cx + 3 + kw / 2, cy + 5, 20, done ? Palette.Shade(Palette.Snow, 0.7f) : Palette.Gold, Align.Center);
            string name = sim.PartName(game.ToyId, part);
            ui.Text(ui.Fit(name, 16, cardW - kw - 12), cx + kw + 9, cy + 8, 16, done ? Palette.Shade(Palette.Snow, 0.8f) : Palette.Ink);
        }

        // The combinations: name, then the keys in turn (or "From memory!" once they are no longer listed).
        float ry = y + Top;
        foreach (var recipe in game.Recipes)
        {
            bool wanted = game.LetterWaiting && game.StoredId(recipe) == game.Wanted && game.Recipes.Count > 1;
            bool alive = game.Fits(recipe);
            var nameColour = !alive ? Palette.Shade(Palette.Snow, 0.45f) : wanted ? Palette.Gold : Palette.Snow;
            float rx = x + 12;
            if (wanted)
            {
                string tag = d.Message("bench_wanted");
                float tw = ui.MeasureText(tag, 11) + 10;
                ui.FillRounded(rx, ry + 2, tw, 15, Palette.Berry, 0.5f);
                ui.Text(tag, rx + 5, ry + 3, 11, Palette.Snow);
                rx += tw + 6;
            }
            string label = recipe.Name.Length > 0 ? recipe.Name : toy?.Name ?? recipe.Id;
            ui.Text(ui.Fit(label, 15, 150), rx, ry + 1, 15, nameColour);
            float kx = x + 190;
            if (!show)
            {
                ui.Text(d.Message("bench_memory"), kx, ry + 1, 15, Palette.Shade(Palette.Snow, alive ? 0.7f : 0.4f));
                ry += RecipeRow;
                continue;
            }
            for (int s = 0; s < recipe.Order.Count; s++)
            {
                int k = game.KeyOf(recipe.Order[s]);
                string key = k > 0 ? input.Label(InputAction.Part1 + (k - 1)) : "?";
                bool done = alive && s < game.Placed, next = alive && s == game.Placed;
                float w = Math.Max(22, ui.MeasureText(key, 15) + 10);
                var bg = !alive ? Palette.WithAlpha(Palette.Snow, 25) : done ? Palette.Gold : next ? Palette.Berry : Palette.WithAlpha(Palette.Snow, 40);
                var fg = !alive ? Palette.Shade(Palette.Snow, 0.45f) : done ? Palette.Ink : Palette.Snow;
                ui.FillRounded(kx, ry, w, 19, bg, 0.4f);
                ui.Text(key, kx + w / 2, ry + 1, 15, fg, Align.Center);
                kx += w;
                if (s < recipe.Order.Count - 1)
                {
                    ui.Text(">", kx + 5, ry + 1, 15, Palette.Shade(Palette.Snow, 0.5f));
                    kx += 18;
                }
            }
            ry += RecipeRow;
        }
    }
}
