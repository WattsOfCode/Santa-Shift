using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>Order schedule, capacity, expiry and shift end (GDD sections 9 and 24).</summary>
public class OrderTests
{
    [Fact]
    public void First_order_is_a_train_with_a_55_second_deadline()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(1);

        var order = Assert.Single(sim.State.LiveOrders);
        Assert.Equal("train", order.ToyId);
        Assert.Equal(sim.Tuning.Ticks(55), order.AllowedDurationTicks);
        Assert.False(order.RequiresRibbon);
        Assert.False(order.IsRush);
    }

    [Fact]
    public void Toys_follow_the_train_bear_robot_sequence()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.WaitSeconds(36);
        sim.Wait(1);

        Assert.Equal(["train", "bear", "robot"], sim.State.LiveOrders.Select(o => o.ToyId));
    }

    [Fact]
    public void A_full_board_skips_the_spawn_and_does_not_count_it_as_missed()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.WaitSeconds(54);
        sim.Wait(1); // spawn attempt at 54 s with three live orders

        Assert.Equal(3, sim.State.LiveOrders.Count);
        Assert.Equal(1, sim.State.SkippedSpawns);
        Assert.Equal(0, sim.State.Missed);
    }

    [Fact]
    public void An_expired_order_is_counted_once_and_resets_the_streak()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(1);
        sim.State.Streak = 3;

        sim.WaitSeconds(56);

        Assert.Equal(1, sim.State.Missed);
        Assert.Equal(0, sim.State.Streak);
        Assert.Equal(OrderStatus.Missed, sim.State.ClosedOrders.Single(o => o.ToyId == "train" && o.Id == 1).Status);

        sim.WaitSeconds(1);
        Assert.Equal(1, sim.State.Missed);
    }

    [Fact]
    public void Expiry_does_not_touch_the_carried_item()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(1);
        sim.MakeToy("train");

        sim.WaitSeconds(56);

        Assert.Equal(ItemStage.Assembled, sim.State.CarriedItem!.Stage);
    }

    [Fact]
    public void Shift_end_closes_remaining_orders_as_unfinished_and_every_order_ends_exactly_once()
    {
        // A 100 s shift ends while orders are still live.
        // (With the real 300 s schedule and nobody delivering, every order expires first.)
        var sim = new Simulation(TestContent.Create(tuning: new SantaShift.Core.Content.Tuning { ShiftSeconds = 100 }), GameMode.Standard);
        sim.Wait(sim.Tuning.ShiftTicks + 1);

        var st = sim.State;
        Assert.Equal(RunPhase.Results, st.Phase);
        Assert.Empty(st.LiveOrders);
        Assert.True(st.Unfinished > 0, $"created={st.OrdersCreated} delivered={st.Delivered} missed={st.Missed} unfinished={st.Unfinished} skipped={st.SkippedSpawns} closed={string.Join(",", st.ClosedOrders.Select(o => $"{o.CreatedTick / 60}:{o.Status}"))}");
        Assert.Equal(st.OrdersCreated, st.Delivered + st.Missed + st.Unfinished);
        Assert.Equal(st.OrdersCreated, st.ClosedOrders.Select(o => o.Id).Distinct().Count());
        Assert.True(st.OrdersCreated <= sim.Tuning.SpawnTimes.Length);
    }

    [Fact]
    public void Relaxed_mode_extends_deadlines_by_half()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Relaxed);
        sim.Wait(1);

        Assert.Equal(sim.Tuning.Ticks(55 * 1.5), sim.State.LiveOrders.Single().AllowedDurationTicks);
        Assert.Equal(6, sim.Quota);
    }

    [Fact]
    public void Orders_take_letters_for_their_toy_in_order_and_wrap()
    {
        var letters = new List<ChildLetter>
        {
            new() { Id = "a", Child = "Ann", ToyId = "train" },
            new() { Id = "b", Child = "Ben", ToyId = "train" },
            new() { Id = "c", Child = "Cat", ToyId = "bear" },
        };
        var content = TestContent.Create(letters);
        var state = new RunState();
        var orders = new OrderSystem(content);

        Assert.Equal("Ann", orders.Create(state).Letter!.Child); // train
        Assert.Equal("Cat", orders.Create(state).Letter!.Child); // bear
        Assert.Null(orders.Create(state).Letter);                // robot: no letters
        Assert.Equal("Ben", orders.Create(state).Letter!.Child); // train
        Assert.Equal("Cat", orders.Create(state).Letter!.Child); // bear (wraps)
        Assert.Null(orders.Create(state).Letter);
        Assert.Equal("Ann", orders.Create(state).Letter!.Child); // train (wraps)
    }
}
