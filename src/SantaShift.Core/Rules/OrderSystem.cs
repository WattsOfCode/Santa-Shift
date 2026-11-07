using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>Creates, delivers, expires and closes orders (GDD sections 8, 9 and 12).</summary>
public sealed class OrderSystem(GameContent content)
{
    private readonly Tuning _t = content.Tuning;

    /// <summary>Runs every scheduled spawn attempt that is due. A full board skips the attempt; nothing is queued.</summary>
    public void SpawnScheduled(RunState state, List<SimEvent> output)
    {
        var times = _t.SpawnTimes;
        while (state.NextSpawnIndex < times.Length && _t.Ticks(times[state.NextSpawnIndex]) <= state.Tick)
        {
            state.NextSpawnIndex++;
            if (state.LiveOrders.Count >= _t.MaxLiveOrders) { state.SkippedSpawns++; continue; }
            output.Add(new SimEvent(SimEventType.OrderCreated, OrderId: Create(state).Id));
        }
    }

    public Order Create(RunState state)
    {
        string toyId = _t.ToySequence[state.OrdersCreated % _t.ToySequence.Length];
        bool ribbon = state.Events.RibbonActive(state.Tick);
        bool rush = state.Events.PendingRush;
        state.Events.PendingRush = false;

        var letter = NextLetter(state, toyId);
        // A child on the naughty list gets coal instead (only if there is a coal station to get it from). Coal never
        // needs a ribbon.
        bool naughty = letter != null && content.HasCoal && state.NaughtyChildren.Contains(letter.Child);
        if (naughty) ribbon = false;

        double seconds = rush ? _t.RushDeadlineSeconds : ribbon ? _t.RibbonDeadlineSeconds : _t.DeadlineSeconds;
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
        // Santa sends one kit per toy asked for, so the bins hold exactly what the letters need (see Tuning.ResupplyKits).
        if (state.LimitedKits && !naughty) state.KitStock[toyId] = state.KitStock.GetValueOrDefault(toyId) + 1;
        return order;
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
