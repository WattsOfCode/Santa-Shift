using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>Shipping rules (GDD sections 8 and 24). The first order (a train, 55 s) appears on the first step.</summary>
public class DeliveryTests
{
    private static Simulation NewSim()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(1); // spawns the first order: train
        return sim;
    }

    [Fact]
    public void Full_loop_collect_assemble_deliver_scores_once()
    {
        var sim = NewSim();
        sim.MakeToy("train");

        var events = sim.Use("shipping");

        Assert.Null(sim.State.CarriedItem);
        Assert.Equal(1, sim.State.Delivered);
        Assert.Empty(sim.State.LiveOrders);
        var delivered = Assert.Single(events, e => e.Type == SimEventType.Delivered);
        Assert.Equal(sim.State.Score, delivered.Points);
        Assert.True(sim.State.Score > 100);
    }

    [Fact]
    public void Second_press_after_delivery_is_rejected_and_scores_nothing()
    {
        var sim = NewSim();
        sim.MakeToy("train");
        sim.Use("shipping");
        int score = sim.State.Score;

        var events = sim.Use("shipping");

        Assert.Equal(score, sim.State.Score);
        Assert.Equal(1, sim.State.Delivered);
        Assert.Contains(events, e => e.Type == SimEventType.Rejected && e.Text == "nothing_to_ship");
    }

    [Fact]
    public void A_kit_is_rejected_and_kept()
    {
        var sim = NewSim();
        sim.Use("kits_train");

        var events = sim.Use("shipping");

        Assert.NotNull(sim.State.CarriedItem);
        Assert.Single(sim.State.LiveOrders);
        Assert.Contains(events, e => e.Text == "assemble_first");
    }

    [Fact]
    public void Wrong_toy_is_rejected_without_changing_score_or_deadline()
    {
        var sim = NewSim();
        var order = sim.State.LiveOrders.Single();
        long deadline = order.DeadlineTick;
        sim.MakeToy("bear");

        var events = sim.Use("shipping");

        Assert.Equal("bear", sim.State.CarriedItem!.ToyId);
        Assert.Equal(0, sim.State.Score);
        Assert.Equal(deadline, order.DeadlineTick);
        Assert.Contains(events, e => e.Text == "no_matching_order");
    }

    [Fact]
    public void Missing_ribbon_is_explained_and_an_extra_ribbon_is_allowed()
    {
        var sim = NewSim();
        sim.State.LiveOrders.Clear();
        var ribbonOrder = AddOrder(sim, "train", requiresRibbon: true);

        sim.MakeToy("train");
        Assert.Contains(sim.Use("shipping"), e => e.Text == "ribbon_needed");
        Assert.NotNull(sim.State.CarriedItem);

        sim.Use("ribbon");
        sim.FinishCrafting();
        sim.Use("shipping");
        Assert.Equal(OrderStatus.Delivered, ribbonOrder.Status);

        // Extra ribbon on an order that doesn't need one: accepted, no extra points.
        var plain = AddOrder(sim, "bear", requiresRibbon: false);
        sim.MakeToy("bear", ribbon: true);
        sim.Use("shipping");
        Assert.Equal(OrderStatus.Delivered, plain.Status);
    }

    [Fact]
    public void Matching_order_with_the_earliest_deadline_is_chosen()
    {
        var sim = NewSim();
        sim.State.LiveOrders.Clear();
        var late = AddOrder(sim, "train", deadlineOffset: 3000);
        var early = AddOrder(sim, "train", deadlineOffset: 1000);

        sim.MakeToy("train");
        sim.Use("shipping");

        Assert.Equal(OrderStatus.Delivered, early.Status);
        Assert.Equal(OrderStatus.Live, late.Status);
    }

    [Fact]
    public void Equal_deadlines_fall_back_to_oldest_then_smallest_id()
    {
        var sim = NewSim();
        sim.State.LiveOrders.Clear();
        var b = AddOrder(sim, "train", deadlineOffset: 1000, createdTick: 5);
        var a = AddOrder(sim, "train", deadlineOffset: 1000, createdTick: 2);

        sim.MakeToy("train");
        sim.Use("shipping");

        Assert.Equal(OrderStatus.Delivered, a.Status);
        Assert.Equal(OrderStatus.Live, b.Status);
    }

    private static Order AddOrder(Simulation sim, string toy, bool requiresRibbon = false, long deadlineOffset = 3300, long? createdTick = null)
    {
        var o = new Order
        {
            Id = sim.State.NextId(),
            ToyId = toy,
            RequiresRibbon = requiresRibbon,
            CreatedTick = createdTick ?? sim.State.Tick,
            DeadlineTick = sim.State.Tick + deadlineOffset,
            AllowedDurationTicks = deadlineOffset,
        };
        sim.State.LiveOrders.Add(o);
        return o;
    }
}
