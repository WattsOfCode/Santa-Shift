using SantaShift.Core.Content;
using SantaShift.Core.Rules;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Views.UI;

/// <summary>
/// The workshop HUD (GDD section 14). Top band: shift clock, order cards, Santa strip.
/// Bottom band: carried item, contextual prompt, score and quota.
/// </summary>
public sealed class HudView(GameContent content)
{
    private readonly OrderCardView _card = new(content);
    private readonly SantaPanelView _santa = new();
    private readonly PromptView _prompt = new();
    private readonly AssemblyPanelView _assembly = new();

    /// <param name="earlierRoundsScore">Score carried over from previous rounds of this run.</param>
    public void Draw(UiCanvas ui, Simulation sim, Feedback feedback, WorldView world, int earlierRoundsScore = 0)
    {
        var state = sim.State;
        var t = sim.Tuning;

        if (!state.IsWorking) world.DrawLabels(ui, sim.SelectedStation()); // labels would cover the close-up work
        DrawProgressBar(ui, sim, world);
        _assembly.Draw(ui, sim, (UiCanvas.Width - AssemblyPanelView.Width) / 2, 486 - AssemblyPanelView.Height - 8);

        // Top band
        // No full-width band: in third person the room shows between the panels.
        double secondsLeft = (double)sim.RemainingShiftTicks / t.TicksPerSecond;
        int whole = (int)Math.Ceiling(secondsLeft);
        ui.FillRounded(10, 8, 150, 72, Palette.WithAlpha(Palette.Ink, 215), 0.2f);
        ui.Text($"ROUND {state.Round}", 85, 14, 14, Palette.Ice, Align.Center);
        ui.Text($"{whole / 60}:{whole % 60:00}", 85, 34, 36, secondsLeft <= 30 ? Palette.Berry : Palette.Snow, Align.Center);

        var orders = state.LiveOrders.OrderBy(o => o.DeadlineTick).ThenBy(o => o.CreatedTick).ThenBy(o => o.Id).ToList();
        for (int i = 0; i < t.MaxLiveOrders && i < 3; i++)
        {
            float x = 172 + i * (OrderCardView.Width + 10);
            if (i < orders.Count) _card.Draw(ui, orders[i], state.Tick, x, 8);
            else ui.OutlineRounded(x, 8, OrderCardView.Width, OrderCardView.Height, Palette.WithAlpha(Palette.Snow, 70), 2, 0.18f);
        }

        _santa.Draw(ui, sim, feedback, 10, 84, UiCanvas.Width - 20, 26);

        // Bottom band
        ui.Fill(0, 486, UiCanvas.Width, 54, Palette.WithAlpha(Palette.Ink, 170));

        ui.Text("CARRYING", 16, 492, 12, Palette.Ice);
        var item = state.CarriedItem;
        if (item != null)
        {
            var toy = content.FindToy(item.ToyId);
            ToyIcon.Draw(ui, item.ToyId, 30, 520, 22, toy != null ? Palette.Hex(toy.Color) : Palette.Wood);
        }
        ui.Text(ui.Fit(sim.ItemName(item), 20, 180), 50, 509, 20, item == null ? Palette.Shade(Palette.Snow, 0.7f) : Palette.Snow);

        _prompt.Draw(ui, sim.Prompt(), feedback, UiCanvas.Width / 2, 497);

        ui.Text($"{earlierRoundsScore + state.Score}", UiCanvas.Width - 16, 490, 24, Palette.Gold, Align.Right);
        ui.Text($"{state.Delivered}/{sim.Quota} delivered", UiCanvas.Width - 16, 516, 16,
            state.Delivered >= sim.Quota ? Palette.Gold : Palette.Snow, Align.Right);
    }

    private static void DrawProgressBar(UiCanvas ui, Simulation sim, WorldView world)
    {
        var action = sim.State.CraftingAction;
        if (action == null) return;
        if (world.AbovePlayer(sim.State) is not { } at) return;
        const float w = 70, h = 10;
        float p = (float)action.Progress(sim.State.Tick);
        ui.FillRounded(at.X - w / 2 - 2, at.Y - 2, w + 4, h + 4, Palette.Ink, 0.5f);
        ui.FillRounded(at.X - w / 2, at.Y, w * p, h, Palette.Gold, 0.5f);
    }
}
