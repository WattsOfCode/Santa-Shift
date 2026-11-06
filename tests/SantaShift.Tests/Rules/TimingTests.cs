using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>One clock for everything, deadline edges, pause, shift end (GDD sections 11 and 24).</summary>
public class TimingTests
{
    /// <summary>Makes a train, then waits so the next step happens on <paramref name="tick"/>.</summary>
    private static Simulation ReadyToShipAt(Func<Order, long> tick)
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(1);
        var order = sim.State.LiveOrders.Single();
        sim.MakeToy("train");
        sim.PlacePlayer(sim.StationById("shipping").Interact);
        while (sim.State.Tick < tick(order)) sim.Step(PlayerCommand.None);
        return sim;
    }

    [Fact]
    public void Delivery_on_the_exact_deadline_tick_is_accepted()
    {
        var sim = ReadyToShipAt(o => o.DeadlineTick);
        sim.Step(new PlayerCommand(0, 0, true));
        Assert.Equal(1, sim.State.Delivered);
        Assert.Equal(0, sim.State.Missed);
    }

    [Fact]
    public void Delivery_one_tick_late_is_not_accepted()
    {
        var sim = ReadyToShipAt(o => o.DeadlineTick + 1);
        sim.Step(new PlayerCommand(0, 0, true));
        Assert.Equal(0, sim.State.Delivered);
        Assert.Equal(1, sim.State.Missed);
        Assert.NotNull(sim.State.CarriedItem); // the toy may still fit a future order
    }

    [Fact]
    public void Pause_stops_the_shift_clock_deadlines_and_crafting()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(1);
        long tick = sim.State.Tick;

        sim.Pause();
        sim.Wait(10_000);

        Assert.Equal(tick, sim.State.Tick);
        Assert.Equal(0, sim.State.Missed);

        sim.Resume();
        sim.Wait(1);
        Assert.Equal(tick + 1, sim.State.Tick);
    }

    [Fact]
    public void Shift_end_takes_precedence_over_a_last_moment_shipment()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(sim.Tuning.ShiftTicks - sim.Tuning.Ticks(10));
        sim.State.LiveOrders.Clear();
        var order = new Order
        {
            Id = sim.State.NextId(), ToyId = "train", CreatedTick = sim.State.Tick,
            DeadlineTick = long.MaxValue / 2, AllowedDurationTicks = 3300,
        };
        sim.State.LiveOrders.Add(order);
        sim.MakeToy("train");
        sim.PlacePlayer(sim.StationById("shipping").Interact);
        while (sim.State.Tick < sim.Tuning.ShiftTicks) sim.Step(PlayerCommand.None);

        sim.Step(new PlayerCommand(0, 0, true));

        Assert.Equal(RunPhase.Results, sim.State.Phase);
        Assert.Equal(0, sim.State.Delivered);
        Assert.Equal(OrderStatus.Unfinished, order.Status);
    }

    [Fact]
    public void Nothing_happens_after_the_shift_ends()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(sim.Tuning.ShiftTicks + 1);
        long tick = sim.State.Tick;

        sim.Wait(100);
        sim.Use("kits_train");

        Assert.Equal(tick, sim.State.Tick);
        Assert.Null(sim.State.CarriedItem);
    }

    [Fact]
    public void Diagonal_movement_is_the_same_speed_as_straight()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        var start = sim.State.PlayerPosition;
        double r = Math.Sqrt(0.5);

        sim.Step(new PlayerCommand(r, -r, false));

        double moved = sim.State.PlayerPosition.DistanceTo(start);
        Assert.Equal(sim.Tuning.PlayerSpeed / sim.Tuning.TicksPerSecond, moved, 6);
    }

    [Fact]
    public void The_elf_cannot_walk_through_stations_or_walls()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        var motion = new PlayerMotion(sim.Content);

        sim.PlacePlayer(sim.StationById("bench").Interact);
        sim.Wait(1);
        for (int i = 0; i < 120; i++) sim.Step(new PlayerCommand(0, -1, false)); // walk into the bench
        var bench = sim.StationById("bench").Body;
        Assert.False(motion.ColliderAt(sim.State.PlayerPosition).Overlaps(bench));

        for (int i = 0; i < 600; i++) sim.Step(new PlayerCommand(1, 0, false)); // walk into the right wall
        Assert.True(sim.State.PlayerPosition.X + sim.Tuning.PlayerColliderWidth / 2 <= sim.Tuning.Floor.Right);
    }

    [Fact]
    public void Selection_picks_the_nearest_station_in_range()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);

        sim.PlacePlayer(sim.StationById("bench").Interact);
        Assert.Equal("bench", sim.SelectedStation()!.Id);

        sim.PlacePlayer(new Point2(470, 410)); // open floor
        Assert.Null(sim.SelectedStation());
    }
}
