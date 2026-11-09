using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>Creates, delivers, expires and closes orders (GDD sections 8, 9 and 12).</summary>
public sealed class OrderSystem(GameContent content)
{
    private readonly Tuning _t = content.Tuning;

    /// <summary>
    /// Runs every scheduled spawn attempt that is due. A full board skips the attempt; nothing is queued. From the second
    /// round, when the board runs low the next letter arrives early, so there is always something to do.
    /// </summary>
    public void SpawnScheduled(RunState state, List<SimEvent> output)
    {
        var times = state.SpawnTicks;
        int max = _t.MaxLiveFor(state.Round);
        while (state.NextSpawnIndex < times.Length && times[state.NextSpawnIndex] <= state.Tick)
        {
            state.NextSpawnIndex++;
            if (state.LiveOrders.Count >= max) { state.SkippedSpawns++; continue; }
            Spawn(state, output);
        }

        int min = _t.MinLiveFor(state.Round);
        if (state.LiveOrders.Count < Math.Min(min, max) && state.NextSpawnIndex < times.Length
            && state.Tick - state.LastSpawnTick >= _t.Ticks(_t.Flow.RefillSeconds))
        {
            state.NextSpawnIndex++;
            Spawn(state, output);
        }
    }

    private void Spawn(RunState state, List<SimEvent> output)
    {
        state.LastSpawnTick = state.Tick;
        output.Add(new SimEvent(SimEventType.OrderCreated, OrderId: Create(state).Id));
    }

    /// <summary>
    /// Decides who writes in this shift (one entry per scheduled order) and, with a supply sled, fills the kit bins from
    /// it in one go: one kit per toy a nice child will ask for. Bins are never topped up during the shift after this;
    /// a wasted kit has to be replaced with a supply crate from the sled. Called once when the shift starts.
    /// Every scheduled attempt is counted, so a slow shift (with skipped attempts) leaves kits over, never short.
    /// </summary>
    public void PlanShift(RunState state)
    {
        state.OrderPlan.Clear();
        state.LetterCursor.Clear();
        state.SpawnTicks = _t.SpawnTicksFor(state.Mode, state.Round);
        var tut = _t.Tutorial;
        string[] sequence = _t.ToySequence;
        for (int i = 0; i < state.SpawnTicks.Length; i++)
        {
            // Lesson round with a short kit: after the lesson, the last lesson order's toy isn't asked for again, so its
            // bin really is empty when that order comes in (see TutorialTuning.ShortKit).
            if (state.Tutorial && LessonShortKit(state) && i == tut.Orders)
            {
                string shortToy = state.OrderPlan[tut.Orders - 1].ToyId;
                if (sequence.Length > 1) sequence = sequence.Where(t => t != shortToy).ToArray();
            }
            int index = state.Tutorial && i >= tut.Orders && sequence != _t.ToySequence ? i - tut.Orders : i;
            state.OrderPlan.Add(PlanOrder(state, i + 1, sequence[index % sequence.Length]));
        }

        if (!state.LimitedKits) return;
        state.KitStock.Clear();
        foreach (var p in state.OrderPlan.Where(p => !p.Naughty))
            state.KitStock[p.ToyId] = state.KitStock.GetValueOrDefault(p.ToyId) + 1;
        if (state.Tutorial && LessonShortKit(state))
        {
            string shortToy = state.OrderPlan[tut.Orders - 1].ToyId;
            state.KitStock[shortToy] = Math.Max(0, state.KitStock.GetValueOrDefault(shortToy) - 1);
        }
    }

    /// <summary>The lesson ends on a plain toy whose bin can start one kit short (needs a sled and a full lesson).</summary>
    private bool LessonShortKit(RunState state) =>
        _t.Tutorial.ShortKit && content.HasSupply && _t.Tutorial.Orders >= 1 && _t.Tutorial.Orders <= state.SpawnTicks.Length
        && state.OrderPlan.Count >= _t.Tutorial.Orders && !state.OrderPlan[_t.Tutorial.Orders - 1].Naughty;

    /// <summary>The <paramref name="n"/>-th order of the shift (1-based): its toy, letter and whether the child is naughty.</summary>
    private PlannedOrder PlanOrder(RunState state, int n, string toyId)
    {
        var letter = NextLetter(state, toyId);
        // A child on the naughty list gets coal instead (only if there is a coal station to get it from).
        bool naughty = letter != null && content.HasCoal && state.NaughtyChildren.Contains(letter.Child);

        // The guided first orders show every job once: one is coal, the rest are plain toys (one needs a ribbon).
        if (state.Tutorial && n <= _t.Tutorial.Orders)
        {
            var naughtyLetter = content.Letters.FirstOrDefault(l => state.NaughtyChildren.Contains(l.Child));
            if (n == _t.Tutorial.CoalOrder && content.HasCoal && naughtyLetter != null)
                (letter, toyId, naughty) = (naughtyLetter, naughtyLetter.ToyId, true);
            else if (naughty) // a naughty child would turn this lesson into coal: write it from a nice child instead
            {
                letter = content.Letters.FirstOrDefault(l => l.ToyId == toyId && !state.NaughtyChildren.Contains(l.Child));
                naughty = false;
            }
        }
        return new PlannedOrder(toyId, letter, naughty);
    }

    public Order Create(RunState state)
    {
        // Past the end of the plan (only when orders are created by hand, as in tests), keep the usual sequence going.
        var plan = state.OrdersCreated < state.OrderPlan.Count
            ? state.OrderPlan[state.OrdersCreated]
            : PlanOrder(state, state.OrdersCreated + 1, _t.ToySequence[state.OrdersCreated % _t.ToySequence.Length]);
        var (toyId, letter, naughty) = (plan.ToyId, plan.Letter, plan.Naughty);
        bool ribbon = state.Events.RibbonActive(state.Tick);
        bool rush = state.RushShift || state.Events.RushActive(state.Tick) || state.Events.PendingRush;
        state.Events.PendingRush = false;

        bool guided = state.Tutorial && state.TutorialOrderIds.Count < _t.Tutorial.Orders;
        if (guided)
        {
            int n = state.TutorialOrderIds.Count + 1;
            ribbon = !naughty && n == _t.Tutorial.RibbonOrder;
            rush = false;
        }
        if (naughty) ribbon = false; // coal never needs a ribbon

        // RUSH takes the same time off either deadline, so a rush order that also needs a ribbon keeps its extra time.
        double seconds = (ribbon ? _t.RibbonDeadlineSeconds : _t.DeadlineSeconds)
            - (rush ? _t.DeadlineSeconds - _t.RushDeadlineSeconds : 0);
        seconds *= _t.DeadlineMultiplier(state.Mode, state.Round) * state.Modifiers.DeadlineMultiplier;
        long allowed = _t.Ticks(seconds);

        var order = new Order
        {
            Id = state.NextId(),
            ToyId = toyId,
            RequiresRibbon = ribbon,
            // Colours take turns, shifted each round so the pattern isn't the same every shift.
            RibbonColor = ribbon && _t.RibbonColors.Length > 0
                ? _t.RibbonColors[(state.OrdersCreated + state.Round) % _t.RibbonColors.Length].Id : null,
            IsRush = rush,
            Naughty = naughty,
            CreatedTick = state.Tick,
            DeadlineTick = state.Tick + allowed,
            AllowedDurationTicks = allowed,
            Letter = letter,
        };
        state.OrdersCreated++;
        state.LiveOrders.Add(order);
        if (guided) state.TutorialOrderIds.Add(order.Id);
        return order; // no kits arrive with it: the bins were stocked for the whole shift in PlanShift
    }

    /// <summary>
    /// The order a carried item would go to: earliest deadline, then oldest, then smallest id.
    /// Coal goes to any naughty child's order; a toy only to a nice child who asked for it.
    /// </summary>
    public Order? FindMatch(RunState state, Item item)
    {
        if (item.Stage is not (ItemStage.Assembled or ItemStage.Coal)) return null;
        return state.LiveOrders
            .Where(o => o.DeadlineTick >= state.Tick)
            .Where(o => item.IsCoal
                ? o.Naughty
                : !o.Naughty && o.ToyId == item.ToyId && RibbonOk(o, item))
            .OrderBy(o => o.DeadlineTick).ThenBy(o => o.CreatedTick).ThenBy(o => o.Id)
            .FirstOrDefault();
    }

    /// <summary>The order wants no ribbon, or the item has one (in the right colour, if a colour was asked for).</summary>
    private static bool RibbonOk(Order o, Item item) =>
        !o.RequiresRibbon || (item.HasRibbon && (o.RibbonColor == null || o.RibbonColor == item.RibbonColor));

    /// <summary>Ships the carried item. A rejection never consumes the item or changes the score.</summary>
    public void TryDeliver(RunState state, List<SimEvent> output)
    {
        var d = content.Dialogue;
        var item = state.CarriedItem;
        if (item == null) { output.Add(Reject(d.Message("nothing_to_ship"))); return; }
        if (item.Stage == ItemStage.Kit) { output.Add(Reject(d.Message("assemble_first"), item.ToyId)); return; }
        if (item.Stage == ItemStage.Botched) { output.Add(Reject(d.Message("botched_scrap_it"), item.ToyId)); return; }

        var order = FindMatch(state, item);
        if (order == null)
        {
            if (item.IsCoal) { output.Add(Reject(d.Message("coal_no_naughty"), item.ToyId)); return; }
            var naughtyAsker = state.LiveOrders.FirstOrDefault(o => o.Naughty && o.ToyId == item.ToyId);
            if (naughtyAsker != null && !state.LiveOrders.Any(o => !o.Naughty && o.ToyId == item.ToyId))
            {
                output.Add(Reject(d.Message("naughty_no_toy", naughtyAsker.Letter?.Child ?? "That child"), item.ToyId));
                return;
            }
            var wrongColour = state.LiveOrders.FirstOrDefault(o => !o.Naughty && o.ToyId == item.ToyId && o.RequiresRibbon
                && item.HasRibbon && o.RibbonColor != null && o.RibbonColor != item.RibbonColor);
            if (wrongColour != null)
            {
                output.Add(Reject(d.Message("wrong_ribbon_colour", wrongColour.Letter?.Child ?? "That child",
                    _t.FindRibbon(wrongColour.RibbonColor)?.Name ?? wrongColour.RibbonColor!), item.ToyId));
                return;
            }
            bool needsRibbon = state.LiveOrders.Any(o => !o.Naughty && o.ToyId == item.ToyId && o.RequiresRibbon && !item.HasRibbon);
            output.Add(Reject(d.Message(needsRibbon ? "ribbon_needed" : "no_matching_order"), item.ToyId));
            return;
        }

        state.Streak++;
        state.BestStreak = Math.Max(state.BestStreak, state.Streak);
        double jolly = _t.Spirit.Enabled && state.Spirit >= _t.Spirit.JollyAt ? 1 + _t.Spirit.JollyBonus : 1;
        int points = (int)Math.Round(Scoring.Points(order, state.Tick, state.Streak, _t.Score) * state.Modifiers.ScoreMultiplier * jolly);
        state.Score += points;
        state.Delivered++;
        if (item.IsCoal) state.CoalDelivered++;
        Close(state, order, OrderStatus.Delivered);
        state.CarriedItem = null;

        string text = item.IsCoal
            ? d.Message("coal_delivered", order.Letter?.Child ?? "", points)
            : d.Message("delivered", content.Toy(item.ToyId).Name, points);
        output.Add(new SimEvent(SimEventType.Delivered, text, ToyId: item.ToyId, OrderId: order.Id, Points: points));

        if (!state.QuotaMetAnnounced && state.Delivered >= _t.QuotaFor(state.Mode, state.Round))
        {
            state.QuotaMetAnnounced = true;
            var cue = d.Cue("quota_met");
            output.Add(new SimEvent(SimEventType.QuotaMet, cue?.Text, cue?.Santa));
        }
    }

    /// <summary>Removes orders whose deadline has passed. A delivery on the exact deadline tick still counts.</summary>
    public void Expire(RunState state, List<SimEvent> output)
    {
        foreach (var order in state.LiveOrders.Where(o => state.Tick > o.DeadlineTick).ToList())
        {
            Close(state, order, OrderStatus.Missed);
            state.Missed++;
            state.Streak = 0;
            output.Add(new SimEvent(SimEventType.OrderExpired, content.Dialogue.Message("order_expired"), OrderId: order.Id));
        }
    }

    /// <summary>At shift end, every remaining order becomes unfinished. No penalty.</summary>
    public void CloseUnfinished(RunState state)
    {
        foreach (var order in state.LiveOrders.ToList())
        {
            Close(state, order, OrderStatus.Unfinished);
            state.Unfinished++;
        }
    }

    private static void Close(RunState state, Order order, OrderStatus status)
    {
        order.Status = status;
        state.LiveOrders.Remove(order);
        state.ClosedOrders.Add(order);
    }

    private ChildLetter? NextLetter(RunState state, string toyId)
    {
        var letters = content.Letters.Where(l => l.ToyId == toyId).ToList();
        if (letters.Count == 0) return null;
        int cursor = state.LetterCursor.GetValueOrDefault(toyId);
        state.LetterCursor[toyId] = cursor + 1;
        return letters[cursor % letters.Count]; // wraps around if the shift needs more letters than exist
    }

    private static SimEvent Reject(string text, string? toyId = null) => new(SimEventType.Rejected, text, ToyId: toyId);
}
