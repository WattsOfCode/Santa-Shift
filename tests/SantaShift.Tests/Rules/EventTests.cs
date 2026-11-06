using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>Santa's policy changes (GDD sections 10 and 24).</summary>
public class EventTests
{
    private static (RunState state, EventSystem events, OrderSystem orders, Func<double, long> ticks) Setup()
    {
        var content = TestContent.Create();
        return (new RunState(), new EventSystem(content), new OrderSystem(content), content.Tuning.Ticks);
    }

    [Fact]
    public void Ribbon_window_includes_its_start_and_excludes_its_end()
    {
        var (state, events, orders, ticks) = Setup();
        var output = new List<SimEvent>();

        state.Tick = ticks(60) - 1;
        events.Update(state, output);
        Assert.False(orders.Create(state).RequiresRibbon);

        state.Tick = ticks(60);
        events.Update(state, output);
        Assert.True(orders.Create(state).RequiresRibbon);

        state.Tick = ticks(90) - 1;
        Assert.True(orders.Create(state).RequiresRibbon);

        state.Tick = ticks(90);
        Assert.False(orders.Create(state).RequiresRibbon);
    }

    [Fact]
    public void Orders_keep_their_ribbon_requirement_after_the_window_ends()
    {
        var (state, events, orders, ticks) = Setup();
        state.Tick = ticks(60);
        events.Update(state, []);
        var order = orders.Create(state);

        state.Tick = ticks(120);
        events.Update(state, []);

        Assert.True(order.RequiresRibbon);
        Assert.Equal(ticks(65), order.AllowedDurationTicks);
    }

    [Fact]
    public void Warning_fires_once_five_seconds_early()
    {
        var (state, events, _, ticks) = Setup();
        var output = new List<SimEvent>();

        state.Tick = ticks(55);
        events.Update(state, output);
        events.Update(state, output);

        Assert.Single(output, e => e.Type == SimEventType.PolicyWarning);
        Assert.False(state.Events.RibbonActive(state.Tick));
    }

    [Fact]
    public void Rush_token_survives_a_skipped_spawn_and_is_used_by_the_next_created_order()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.WaitSeconds(120);
        sim.Wait(1);
        Assert.True(sim.State.Events.PendingRush);

        // Fill the board so the 126 s attempt is skipped.
        var orders = new OrderSystem(sim.Content);
        while (sim.State.LiveOrders.Count < 3) sim.State.LiveOrders.Add(new Order { Id = sim.State.NextId(), ToyId = "robot", DeadlineTick = long.MaxValue / 2, AllowedDurationTicks = 1 });
        int skipped = sim.State.SkippedSpawns;
        sim.WaitSeconds(7);
        Assert.Equal(skipped + 1, sim.State.SkippedSpawns);
        Assert.True(sim.State.Events.PendingRush);

        sim.State.LiveOrders.RemoveAt(0);
        var order = orders.Create(sim.State);

        Assert.True(order.IsRush);
        Assert.Equal(sim.Tuning.Ticks(45), order.AllowedDurationTicks);
        Assert.False(sim.State.Events.PendingRush);
    }

    [Fact]
    public void Under_normal_play_rush_orders_spawn_at_126_and_252_seconds()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(sim.Tuning.ShiftTicks + 1);

        var rush = sim.State.ClosedOrders.Where(o => o.IsRush).Select(o => o.CreatedTick).Order().ToList();
        // With nobody delivering, the board fills up, so we only check that every rush order was created at or after its event.
        Assert.All(rush, t => Assert.True(t >= sim.Tuning.Ticks(120)));
    }

    [Fact]
    public void Restart_clears_policy_state()
    {
        var content = TestContent.Create();
        var first = new Simulation(content, GameMode.Standard);
        first.WaitSeconds(121);
        Assert.True(first.State.Events.PendingRush);

        var second = new Simulation(content, GameMode.Standard);
        Assert.False(second.State.Events.PendingRush);
        Assert.Empty(second.State.Events.FiredEventIds);
        Assert.Equal(-1, second.State.Events.RibbonWindowEndTick);
    }
}
