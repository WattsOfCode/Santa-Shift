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

    /// <summary>Hit ids of the open toy cart: <see cref="CartSlotHit"/> + n for toy n (0-based), then the push button.</summary>
    public const int CartSlotHit = 10, CartPushHit = 20;

    /// <summary>The open cart's card or button under the mouse (a hit id above), or -1.</summary>
    public int CartHovered { get; set; } = -1;

    /// <param name="earlierRoundsScore">Score carried over from previous rounds of this run.</param>
    /// <param name="controls">The player's bindings, for the guided round's controls card (null: no card).</param>
    public void Draw(UiCanvas ui, Simulation sim, Feedback feedback, WorldView world, int earlierRoundsScore = 0,
        ControlBindings? controls = null)
    {
        var state = sim.State;
        var t = sim.Tuning;
        Hits.Clear();

        if (!state.IsWorking) world.DrawLabels(ui, sim.SelectedStation(), state, sim.CartSelected()); // labels would cover the close-up work
        DrawProgressBar(ui, sim, world);
        _assembly.Draw(ui, sim, input, (UiCanvas.Width - AssemblyPanelView.Width) / 2, 486 - 8);
        if (sim.State.Assembly != null) world.DrawPieceLabels(ui, sim.State, i => input.Label(InputAction.Part1 + i));
        DrawRibbonPick(ui, sim);
        DrawCartPick(ui, sim);
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
        ui.Text($"{whole / 60}:{whole % 60:00}", 85, 32, 34, secondsLeft <= 30 ? Palette.Berry : Palette.Snow, Align.Center);

        // The Christmas Spirit wreath in the top-right corner; the letters fill the space between it and the clock.
        bool spirit = t.Spirit.Enabled;
        if (spirit) DrawSpirit(ui, sim);
        float lettersRight = spirit ? WreathX - WreathR - 12 : UiCanvas.Width - 10;

        // Up to three full cards; when more letters are waiting (from round 2 the board holds up to ten), compact cards.
        var orders = state.LiveOrders.OrderBy(o => o.DeadlineTick).ThenBy(o => o.CreatedTick).ThenBy(o => o.Id).ToList();
        if (orders.Count <= 3)
        {
            float cw = Math.Min(OrderCardView.Width, (lettersRight - 172 - 20) / 3);
            for (int i = 0; i < t.MaxLiveFor(state.Round) && i < 3; i++)
            {
                float x = 172 + i * (cw + 10);
                if (i < orders.Count) _card.Draw(ui, orders[i], state.Tick, x, 8, cw);
                else ui.OutlineRounded(x, 8, cw, OrderCardView.Height, Palette.WithAlpha(Palette.Snow, 70), 2, 0.18f);
            }
        }
        else
        {
            float left = 172, right = lettersRight, gap = 6;
            float w = Math.Min(OrderCardView.CompactMaxWidth, (right - left - gap * (orders.Count - 1)) / orders.Count);
            for (int i = 0; i < orders.Count; i++) _card.DrawCompact(ui, orders[i], state.Tick, left + i * (w + gap), 8, w);
        }

        _santa.Draw(ui, sim, feedback, 10, 84, UiCanvas.Width - 20, 26);

        // Bottom band
        ui.Fill(0, 486, UiCanvas.Width, 54, Palette.WithAlpha(Palette.Ink, 170));

        var item = state.CarriedItem;
        if (state.Cart.Pushing)
        {
            // Pushing: the toys riding in the cart, as small icons.
            ui.Text("PUSHING THE TOY CART", 16, 492, 12, Palette.Ice);
            var ridden = state.Cart.Items;
            for (int i = 0; i < t.Cart.Capacity; i++)
            {
                float ix = 30 + i * 34;
                if (i < ridden.Count && content.FindToy(ridden[i].ToyId) is { } toy) ToyIcon.Draw(ui, toy.Id, ix, 520, 22, Palette.Hex(toy.Color));
                else ui.OutlineRounded(ix - 11, 509, 22, 22, Palette.WithAlpha(Palette.Snow, 70), 1.5f, 0.3f);
            }
            ui.Text($"{ridden.Count}/{t.Cart.Capacity}", 30 + t.Cart.Capacity * 34 - 6, 509, 20, Palette.Snow);
        }
        else
        {
            ui.Text("CARRYING", 16, 492, 12, Palette.Ice);
            if (item != null)
            {
                var toy = content.FindToy(item.ToyId);
                ToyIcon.Draw(ui, item.ToyId, 30, 520, 22, toy != null ? Palette.Hex(toy.Color) : Palette.Wood);
            }
            ui.Text(ui.Fit(sim.ItemName(item), 20, 180), 50, 509, 20, item == null ? Palette.Shade(Palette.Snow, 0.7f) : Palette.Snow);
        }

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

    /// <summary>
    /// The open toy cart: a card per slot (key, toy icon, name; click or press its key to take it), and a button to grab
    /// the handle and push the cart.
    /// </summary>
    private void DrawCartPick(UiCanvas ui, Simulation sim)
    {
        var cart = sim.State.Cart;
        if (!cart.PickOpen) return;
        var d = content.Dialogue;
        int slots = sim.Tuning.Cart.Capacity;
        const float w = 440, h = 112, x = (UiCanvas.Width - w) / 2, y = 486 - h - 8, gap = 8;
        ui.FillRounded(x, y, w, h, Palette.WithAlpha(Palette.Ink, 225), 0.15f);
        ui.Text($"{d.Message("cart_title")}  {cart.Items.Count}/{slots}", x + 12, y + 7, 14, Palette.Ice);

        float cw = (w - 24 - gap * (slots - 1)) / slots, ch = 40, cy = y + 28;
        for (int i = 0; i < slots; i++)
        {
            float cx = x + 12 + i * (cw + gap);
            if (i >= cart.Items.Count)
            {
                ui.OutlineRounded(cx, cy, cw, ch, Palette.WithAlpha(Palette.Snow, 60), 1.5f, 0.25f);
                ui.Text("empty", cx + cw / 2, cy + 11, 15, Palette.Shade(Palette.Snow, 0.5f), Align.Center);
                continue;
            }
            var item = cart.Items[i];
            bool hot = CartHovered == CartSlotHit + i;
            ui.FillRounded(cx, cy, cw, ch, hot ? Palette.Gold : Palette.Snow, 0.25f);
            Hits.Add(CartSlotHit + i, cx, cy, cw, ch);
            string key = input.Label(InputAction.Part1 + i);
            float kw = Math.Max(24, ui.MeasureText(key, 18) + 10);
            ui.FillRounded(cx + 4, cy + 4, kw, ch - 8, Palette.Ink, 0.3f);
            ui.Text(key, cx + 4 + kw / 2, cy + 9, 18, Palette.Gold, Align.Center);
            var toy = content.FindToy(item.ToyId);
            ToyIcon.Draw(ui, item.ToyId, cx + kw + 20, cy + ch / 2, 22, toy != null ? Palette.Hex(toy.Color) : Palette.Wood);
            string name = toy?.NameOf(item.Variant) ?? item.ToyId;
            float tx = cx + kw + 34;
            if (item.HasRibbon) // a ribbon shows as a little tab in its colour
            {
                var r = sim.Tuning.FindRibbon(item.RibbonColor);
                ui.FillRounded(cx + cw - 14, cy + 6, 8, ch - 12, r != null ? Palette.Hex(r.Color) : Palette.Gold, 0.4f);
            }
            ui.Text(ui.Fit(name, 15, cw - (tx - cx) - 16), tx, cy + 12, 15, Palette.Ink);
        }

        bool pushHot = CartHovered == CartPushHit;
        float by = cy + ch + 8;
        ui.FillRounded(x + 12, by, w - 24, 26, pushHot ? Palette.Gold : Palette.WithAlpha(Palette.Snow, 35), 0.4f);
        Hits.Add(CartPushHit, x + 12, by, w - 24, 26);
        ui.Text($"{input.Label(InputAction.Interact)}  {d.Message("cart_push_button")}", x + w / 2, by + 4, 16,
            pushHot ? Palette.Ink : Palette.Snow, Align.Center);
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

    /// <summary>The spirit wreath's outer radius and centre: in the top-right corner, as tall as the clock panel.</summary>
    private const float WreathR = 37, WreathX = UiCanvas.Width - 10 - WreathR, WreathY = 8 + WreathR;

    /// <summary>
    /// The Christmas Spirit meter, top right: a holly wreath with berries and a red bow, the spirit filling a ring inside
    /// it clockwise from the top, and the percent in the middle. JOLLY turns the ring and number gold (the bonus shows
    /// under the number) and the wreath glows; low spirit turns them red and pulses.
    /// </summary>
    private void DrawSpirit(UiCanvas ui, Simulation sim)
    {
        var s = sim.Tuning.Spirit;
        float frac = Math.Clamp((float)(sim.State.Spirit / s.Max), 0f, 1f);
        bool jolly = sim.State.Spirit >= s.JollyAt, low = frac < 0.3f;
        bool pulse = low && (sim.State.Tick / 15) % 2 == 0;
        bool still = WorldEffects.ReducedMotion;
        float cx = WreathX, cy = WreathY, R = WreathR;
        var centre = new System.Numerics.Vector2(cx, cy);

        // A dark disc behind, so the wreath reads against the room; a warm glow round it while jolly.
        if (jolly) ui.Circle(cx, cy, R + 3 + (still ? 0 : 1.5f * MathF.Sin(sim.State.Tick * 0.12f)), Palette.WithAlpha(Palette.Gold, 70));
        ui.Circle(cx, cy, R, Palette.WithAlpha(Palette.Ink, 225));

        // The wreath: holly leaves all the way round, alternately leaning out and in, darker ones underneath.
        const int leaves = 22;
        for (int layer = 0; layer < 2; layer++)
            for (int i = 0; i < leaves; i++)
            {
                float a = (i + layer * 0.5f) / leaves * MathF.Tau;
                var radial = new System.Numerics.Vector2(MathF.Cos(a), MathF.Sin(a));
                var tangent = new System.Numerics.Vector2(-radial.Y, radial.X);
                float lean = (i % 2 == 0 ? 0.45f : -0.45f);
                var dir = System.Numerics.Vector2.Normalize(tangent + radial * lean);
                var root = centre + radial * (R - 9) - dir * 6;
                var leaf = layer == 0 ? Festive.HollyDark : i % 3 == 0 ? Festive.HollyLight : Festive.Holly;
                Festive.HollyLeaf(ui, root, dir, 14, 4.2f, leaf, Festive.HollyDark);
            }
        // Berry clusters round the ring.
        for (int i = 0; i < 6; i++)
        {
            float a = (i + 0.25f) / 6 * MathF.Tau - MathF.PI / 2;
            var p = centre + new System.Numerics.Vector2(MathF.Cos(a), MathF.Sin(a)) * (R - 7);
            Festive.Berry(ui, p.X, p.Y, 3f);
            Festive.Berry(ui, p.X + 3.5f, p.Y + 2.5f, 2.6f);
        }

        // The meter: a ring inside the wreath, filling clockwise from the top.
        const float inner = 17, outer = 23;
        RingArc(ui, cx, cy, inner - 1.5f, outer + 1.5f, 0, MathF.Tau, Palette.Shade(Palette.Ink, 0.55f));
        RingArc(ui, cx, cy, inner, outer, 0, MathF.Tau, Palette.WithAlpha(Palette.Snow, 28));
        var fillColour = jolly ? Palette.Gold : low ? (pulse ? Palette.Shade(Palette.Berry, 1.35f) : Palette.Berry) : Festive.CaneRed;
        if (frac > 0) RingArc(ui, cx, cy, inner, outer, 0, frac * MathF.Tau, fillColour);
        if (frac > 0.02f) RingArc(ui, cx, cy, outer - 2, outer - 0.5f, 0, frac * MathF.Tau, Palette.WithAlpha(Raylib_cs.Color.White, 90)); // shine

        // The middle: the spirit in percent, with JOLLY's bonus (or "SPIRIT") under it.
        ui.Circle(cx, cy, inner - 1.5f, Palette.Ink);
        var number = jolly ? Palette.Gold : low ? Palette.Berry : Palette.Snow;
        ui.Text($"{sim.State.Spirit:0}%", cx, cy - 10, 14, number, Align.Center);
        string under = jolly ? $"+{(int)Math.Round(s.JollyBonus * 100)}%" : "SPIRIT";
        ui.Text(under, cx, cy + 3, 9, jolly ? Palette.Gold : Palette.Ice, Align.Center);

        // A red bow at the bottom, its tails hanging below the wreath.
        float by = cy + R - 4;
        var knot = new System.Numerics.Vector2(cx, by);
        var bow = Festive.CaneRed;
        var shade = Palette.Shade(bow, 0.75f);
        ui.Triangle(knot, knot + new System.Numerics.Vector2(-13, -7), knot + new System.Numerics.Vector2(-13, 6), bow);
        ui.Triangle(knot, knot + new System.Numerics.Vector2(13, -7), knot + new System.Numerics.Vector2(13, 6), bow);
        ui.Triangle(knot, knot + new System.Numerics.Vector2(-4, 10), knot + new System.Numerics.Vector2(-9, 12), shade);
        ui.Triangle(knot, knot + new System.Numerics.Vector2(4, 10), knot + new System.Numerics.Vector2(9, 12), shade);
        ui.Circle(cx, by, 3.5f, Palette.Shade(bow, 0.85f));

        // A star on top that twinkles while jolly.
        float twinkle = jolly && !still ? 1f + 0.15f * MathF.Sin(sim.State.Tick * 0.15f) : 1f;
        DrawStar(ui, cx, cy - R + 2, 7 * twinkle, jolly ? Palette.Gold : low ? Palette.Berry : Palette.Shade(Palette.Gold, 0.85f));
    }

    /// <summary>A filled ring segment from <paramref name="a0"/> to <paramref name="a1"/> radians, clockwise from 12 o'clock.</summary>
    private static void RingArc(UiCanvas ui, float cx, float cy, float r0, float r1, float a0, float a1, Raylib_cs.Color c)
    {
        int steps = Math.Max(2, (int)MathF.Ceiling((a1 - a0) / (MathF.Tau / 48)));
        System.Numerics.Vector2 P(float a, float r) => new(cx + MathF.Sin(a) * r, cy - MathF.Cos(a) * r);
        for (int i = 0; i < steps; i++)
        {
            float t0 = a0 + (a1 - a0) * i / steps, t1 = a0 + (a1 - a0) * (i + 1) / steps;
            ui.Triangle(P(t0, r0), P(t0, r1), P(t1, r1), c);
            ui.Triangle(P(t0, r0), P(t1, r1), P(t1, r0), c);
        }
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
