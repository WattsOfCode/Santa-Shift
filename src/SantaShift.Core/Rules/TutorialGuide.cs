using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>One step of the guided first round: where to go next, and a short hint (a dialogue key).</summary>
public sealed record GuideStep(Station Station, string HintKey);

/// <summary>
/// The guided first round: works out the next station for the elf from what it holds and which order comes first, so
/// the arrows on the floor can lead there. It only reads the state; the rules are the same as ever.
/// </summary>
public static class TutorialGuide
{
    /// <summary>How many guided orders are finished (delivered or missed).</summary>
    public static int Done(RunState s) => s.ClosedOrders.Count(o => s.TutorialOrderIds.Contains(o.Id));

    /// <summary>True while guided orders are still to come or still open.</summary>
    public static bool Active(Simulation sim) =>
        sim.State.Tutorial && Done(sim.State) < sim.Tuning.Tutorial.Orders;

    /// <summary>The next station to visit, or null (nothing to do yet, busy at a station, or the guide is over).</summary>
    public static GuideStep? Next(Simulation sim)
    {
        var s = sim.State;
        if (!Active(sim) || s.IsWorking || s.Phase != RunPhase.Playing) return null;
        var c = sim.Content;
        Station? Find(StationType type, string? toy = null) =>
            c.Stations.FirstOrDefault(st => st.Type == type && (toy == null || st.ToyId == toy));
        GuideStep? Go(Station? st, string hint) => st == null ? null : new GuideStep(st, hint);

        // The order to work on: the guided ones first, soonest deadline first.
        var order = s.LiveOrders.OrderBy(o => s.TutorialOrderIds.Contains(o.Id) ? 0 : 1)
            .ThenBy(o => o.DeadlineTick).ThenBy(o => o.Id).FirstOrDefault();
        var item = s.CarriedItem;

        if (item == null)
        {
            if (order == null) return null;
            if (order.Naughty) return Go(Find(StationType.Coal), "guide_coal");
            if (s.KitsLeft(order.ToyId) is <= 0) return Go(Find(StationType.Supply), "guide_supply");
            return Go(Find(StationType.Dispenser, order.ToyId), "guide_kit");
        }
        if (item.IsSupply)
        {
            // Restock the bin that is short of what the letters want.
            string toy = s.LiveOrders.Where(o => !o.Naughty && s.KitsLeft(o.ToyId) is <= 0).Select(o => o.ToyId).FirstOrDefault()
                ?? order?.ToyId ?? c.Toys[0].Id;
            return Go(Find(StationType.Dispenser, toy), "guide_restock");
        }
        if (item.Stage == ItemStage.Botched) return Go(Find(StationType.Recycle), "guide_scrap");
        if (item.Stage == ItemStage.Kit) return Go(Find(StationType.Bench), "guide_build");
        if (item.IsCoal) return Go(Find(s.LiveOrders.Any(o => o.Naughty) ? StationType.Shipping : StationType.Recycle), "guide_ship");

        // An assembled toy: a ribbon first if the order it is for wants one.
        var forIt = s.LiveOrders.Where(o => !o.Naughty && o.ToyId == item.ToyId).OrderBy(o => o.DeadlineTick).ToList();
        if (forIt.Count > 0 && !forIt.Any(o => !o.RequiresRibbon) && !item.HasRibbon)
            return Go(Find(StationType.Ribbon), "guide_ribbon");
        return Go(Find(forIt.Count > 0 ? StationType.Shipping : StationType.Recycle), forIt.Count > 0 ? "guide_ship" : "guide_scrap");
    }
}
