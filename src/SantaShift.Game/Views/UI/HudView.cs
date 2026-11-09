using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;
using SantaShift.Game.Services;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Views.UI;

/// <summary>
/// The workshop HUD (GDD section 14). Top band: shift clock, order cards, Santa strip.
/// Bottom band: carried item, contextual prompt, score and quota.
/// </summary>
public sealed class HudView(GameContent content, IInputService input)
{
    private readonly OrderCardView _card = new(content);
    private readonly SantaPanelView _santa = new();
    private readonly PromptView _prompt = new();
    private readonly AssemblyPanelView _assembly = new();

    /// <summary>The punch-clock / recycle question's buttons drawn last: 0 confirms, 1 cancels.</summary>
    public UiHits Hits { get; } = new();

    /// <summary>The question button under the mouse (0 or 1), drawn gold.</summary>
    public int ConfirmHovered { get; set; } = -1;

    /// <param name="earlierRoundsScore">Score carried over from previous rounds of this run.</param>
    /// <param name="controls">The player's bindings, for the guided round's controls card (null: no card).</param>
    public void Draw(UiCanvas ui, Simulation sim, Feedback feedback, WorldView world, int earlierRoundsScore = 0,
        ControlBindings? controls = null)
    {
        var state = sim.State;
        var t = sim.Tuning;
        Hits.Clear();

        if (!state.IsWorking) world.DrawLabels(ui, sim.SelectedStation(), state); // labels would cover the close-up work
        DrawProgressBar(ui, sim, world);
        _assembly.Draw(ui, sim, input, (UiCanvas.Width - AssemblyPanelView.Width) / 2, 486 - AssemblyPanelView.Height - 8);
        DrawRibbonPick(ui, sim);
        DrawGuide(ui, sim);
        bool asking = state.PunchPromptOpen || state.RecyclePromptOpen;
        if (controls != null && !asking) DrawControlsCard(ui, sim, controls);
        if (asking) DrawQuestionButtons(ui, state.PunchPromptOpen);

        // Top band
        // No full-width band: in third person the room shows between the panels.
        double secondsLeft = (double)sim.RemainingShiftTicks / t.TicksPerSecond;
        int whole = (int)Math.Ceiling(secondsLeft);
        ui.FillRounded(10, 8, 150, 72, Palette.WithAlpha(Palette.Ink, 215), 0.2f);
        ui.Text($"ROUND {state.Round}", 20, 13, 14, Palette.Ice);
        ui.Text($"{whole / 60}:{whole % 60:00}", 85, 29, 32, secondsLeft <= 30 ? Palette.Berry : Palette.Snow, Align.Center);
        DrawSpirit(ui, sim, 18, 64, 134);

        // Up to three full cards; when more letters are waiting (from round 2 the board holds up to ten), compact cards.
        var orders = state.LiveOrders.OrderBy(o => o.DeadlineTick).ThenBy(o => o.CreatedTick).ThenBy(o => o.Id).ToList();
        if (orders.Count <= 3)
        {
            for (int i = 0; i < t.MaxLiveFor(state.Round) && i < 3; i++)
            {
                float x = 172 + i * (OrderCardView.Width + 10);
                if (i < orders.Count) _card.Draw(ui, orders[i], state.Tick, x, 8);
                else ui.OutlineRounded(x, 8, OrderCardView.Width, OrderCardView.Height, Palette.WithAlpha(Palette.Snow, 70), 2, 0.18f);
            }
        }
        else
        {
            const float left = 172, right = UiCanvas.Width - 10, gap = 6;
            float w = Math.Min(OrderCardView.CompactMaxWidth, (right - left - gap * (orders.Count - 1)) / orders.Count);
            for (int i = 0; i < orders.Count; i++) _card.DrawCompact(ui, orders[i], state.Tick, left + i * (w + gap), 8, w);
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

        _prompt.Draw(ui, sim.Prompt()?.Replace("[use]", input.Label(InputAction.Interact)), feedback, UiCanvas.Width / 2, 497);

        ui.Text($"{earlierRoundsScore + state.Score}", UiCanvas.Width - 16, 490, 24, Palette.Gold, Align.Right);
        ui.Text($"{state.Delivered}/{sim.Quota} delivered", UiCanvas.Width - 16, 516, 16,
            state.Delivered >= sim.Quota ? Palette.Gold : Palette.Snow, Align.Right);
    }

    /// <summary>
    /// While choosing a ribbon: which colours the waiting letters for this toy ask for, so the player knows what to pick.
    /// </summary>
    private void DrawRibbonPick(UiCanvas ui, Simulation sim)
    {
        var state = sim.State;
        if (state.RibbonPick == null || state.CarriedItem is not { } item) return;
        var t = sim.Tuning;
        var wants = state.LiveOrders.Where(o => !o.Naughty && o.ToyId == item.ToyId && o.RequiresRibbon)
            .OrderBy(o => o.DeadlineTick).Take(3).ToList();
        const float w = 440, h = 62, x = (UiCanvas.Width - w) / 2, y = 486 - h - 8;
        ui.FillRounded(x, y, w, h, Palette.WithAlpha(Palette.Ink, 225), 0.18f);
        string toy = content.FindToy(item.ToyId)?.Name ?? item.ToyId;
        ui.Text($"PICK A RIBBON FOR THE {toy.ToUpperInvariant()}", x + 12, y + 7, 14, Palette.Ice);
        if (wants.Count == 0)
        {
            ui.Text("No letter asks for a colour, so any ribbon will do.", x + 12, y + 32, 16, Palette.Snow);
            return;
        }
        float tx = x + 12;
        ui.Text("WANTED", tx, y + 34, 12, Palette.Ice);
        tx += ui.MeasureText("WANTED", 12) + 10;
        foreach (var o in wants)
        {
            var r = t.FindRibbon(o.RibbonColor);
            string text = $"{r?.Name ?? "Any"} for {o.Letter?.Child ?? "#" + o.Id}";
            float tw = ui.MeasureText(text, 16) + 26;
            ui.FillRounded(tx, y + 29, tw, 24, Palette.WithAlpha(Palette.Snow, 30), 0.5f);
            ui.FillRounded(tx + 5, y + 34, 12, 14, r != null ? Palette.Hex(r.Color) : Palette.Gold, 0.4f);
            ui.Text(text, tx + 21, y + 32, 16, Palette.Snow);
            tx += tw + 8;
        }
    }

    /// <summary>The guided first round: which lesson this is and what to do now, under the Santa strip.</summary>
    private void DrawGuide(UiCanvas ui, Simulation sim)
    {
        if (!TutorialGuide.Active(sim) || sim.State.IsWorking) return;
        var d = content.Dialogue;
        int done = TutorialGuide.Done(sim.State), total = sim.Tuning.Tutorial.Orders;
        string hint = d.Message(TutorialGuide.Next(sim)?.HintKey ?? "guide_wait");
        string title = d.Message("guide_title", Math.Min(done + 1, total), total);
        float tw = ui.MeasureText(title, 14), hw = ui.MeasureText(hint, 17);
        float w = tw + hw + 40, x = 10, y = 116;
        ui.FillRounded(x, y, w, 28, Palette.WithAlpha(Palette.Ink, 215), 0.4f);
        ui.FillRounded(x + 4, y + 4, tw + 14, 20, Palette.Gold, 0.5f);
        ui.Text(title, x + 11, y + 7, 14, Palette.Ink);
        ui.Text(hint, x + tw + 28, y + 5, 17, Palette.Snow);
    }

    /// <summary>
    /// Buttons for the punch-clock and recycle questions, just above the bottom band, so they can be answered with the
    /// mouse too (the pointer is freed while one is open). The keys still work: use confirms, moving or Esc cancels.
    /// </summary>
    private void DrawQuestionButtons(UiCanvas ui, bool punch)
    {
        var d = content.Dialogue;
        string[] labels = punch
            ? [d.Message("button_punch_out"), d.Message("button_keep_working")]
            : [d.Message("button_recycle"), d.Message("button_keep_it")];
        const float w = 170, h = 36, gap = 14, y = 486 - h - 10;
        for (int i = 0; i < 2; i++)
        {
            float x = UiCanvas.Width / 2f + (i == 0 ? -gap / 2 - w : gap / 2);
            bool hot = ConfirmHovered == i;
            ui.FillRounded(x, y, w, h, hot ? Palette.Gold : Palette.WithAlpha(Palette.Ink, 225), 0.45f);
            ui.OutlineRounded(x, y, w, h, Palette.WithAlpha(i == 0 ? Palette.Gold : Palette.Snow, 150), 1.5f, 0.45f);
            ui.Text(labels[i], x + w / 2, y + 8, 18, hot ? Palette.Ink : i == 0 ? Palette.Gold : Palette.Snow, Align.Center);
            Hits.Add(i, x, y, w, h);
        }
    }

    /// <summary>
    /// The guided round's reminder, in the bottom-left corner above the bottom band: how to move and look (with the
    /// player's own keys, or the sticks on a controller), and where to change the controls.
    /// </summary>
    private void DrawControlsCard(UiCanvas ui, Simulation sim, ControlBindings c)
    {
        if (!TutorialGuide.Active(sim) || sim.State.IsWorking) return;
        var d = content.Dialogue;
        string K(string bind) => InputNames.Key(Enumerable.Range(0, ControlBindings.KeySlots).Select(i => c.KeyAt(bind, i))
            .FirstOrDefault(k => k != "") ?? "");
        string move = input.UsingGamepad ? "Left stick"
            : $"{K(ControlBindings.MoveUp)} {K(ControlBindings.MoveLeft)} {K(ControlBindings.MoveDown)} {K(ControlBindings.MoveRight)}";
        string look = input.UsingGamepad ? "Right stick" : "Mouse";
        string line1 = d.Message("guide_move", move, look);
        string line2 = d.Message("guide_keys", input.Label(InputAction.Pause));
        float w = Math.Max(ui.MeasureText(line1, 15), ui.MeasureText(line2, 13)) + 24, h = 46, x = 10, y = 486 - h - 8;
        ui.FillRounded(x, y, w, h, Palette.WithAlpha(Palette.Ink, 215), 0.25f);
        ui.Text(line1, x + 12, y + 7, 15, Palette.Snow);
        ui.Text(line2, x + 12, y + 27, 13, Palette.Ice);
    }

    /// <summary>
    /// The Christmas Spirit meter under the clock: a star and a bar. Gold and "JOLLY" when high (bonus points), red and
    /// pulsing when nearly gone.
    /// </summary>
    private void DrawSpirit(UiCanvas ui, Simulation sim, float x, float y, float w)
    {
        var s = sim.Tuning.Spirit;
        if (!s.Enabled) return;
        float frac = (float)(sim.State.Spirit / s.Max);
        bool jolly = sim.State.Spirit >= s.JollyAt, low = frac < 0.3f;
        var colour = jolly ? Palette.Gold : low ? Palette.Berry : Palette.Pine;
        if (low && (sim.State.Tick / 15) % 2 == 0) colour = Palette.Shade(colour, 1.35f);
        float bx = x + 16, bw = w - 16;
        ui.FillRounded(bx, y, bw, 9, Palette.WithAlpha(Palette.Snow, 35), 0.6f);
        ui.FillRounded(bx, y, bw * frac, 9, colour, 0.6f);
        DrawStar(ui, x + 5, y + 4, 7, jolly ? Palette.Gold : Palette.Snow);
        if (jolly) ui.Text(content.Dialogue.Message("spirit_jolly", (int)Math.Round(s.JollyBonus * 100)), 156, 12, 10, Palette.Gold, Align.Right); // top-right of the clock panel
    }

    private static void DrawStar(UiCanvas ui, float cx, float cy, float r, Raylib_cs.Color c)
    {
        var centre = new System.Numerics.Vector2(cx, cy);
        for (int i = 0; i < 10; i++)
        {
            float a0 = -MathF.PI / 2 + i * MathF.PI / 5, a1 = a0 + MathF.PI / 5;
            float r0 = i % 2 == 0 ? r : r * 0.45f, r1 = i % 2 == 0 ? r * 0.45f : r;
            ui.Triangle(centre, centre + new System.Numerics.Vector2(MathF.Cos(a0), MathF.Sin(a0)) * r0,
                centre + new System.Numerics.Vector2(MathF.Cos(a1), MathF.Sin(a1)) * r1, c);
        }
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
