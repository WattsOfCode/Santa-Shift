using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>
/// From round 2 the training wheels come off: a steady flow of letters on a board of up to ten, never running dry.
/// Also rush stretches, rush shifts and the assembly policy notices.
/// </summary>
public class FlowTests
{
    private static Simulation Sim(int round, Tuning? tuning = null, bool supply = false) =>
        new(TestContent.Create(tuning: tuning, supply: supply), GameMode.Standard, round: round);

    [Fact]
    public void Round_1_keeps_the_small_board_and_round_2_opens_it_up()
    {
        var t = new Tuning();
        Assert.Equal(3, t.MaxLiveFor(1));
        Assert.Equal(0, t.MinLiveFor(1));
        Assert.Equal(10, t.MaxLiveFor(2));
        Assert.Equal(2, t.MinLiveFor(2));
        Assert.Equal(t.SpawnTimes.Length, t.SpawnTicksFor(GameMode.Standard, 1).Length);
    }

    [Fact]
    public void Round_2_starts_with_two_letters()
    {
        var sim = Sim(2);
        sim.Wait(1);
        Assert.Equal(2, sim.State.LiveOrders.Count);
    }

    [Fact]
    public void When_the_board_runs_low_the_next_letter_comes_early()
    {
        var sim = Sim(2);
        sim.Wait(1);
        sim.State.LiveOrders.Clear(); // as if both were shipped at once

        sim.WaitSeconds(sim.Tuning.Flow.RefillSeconds + 0.1);
        Assert.Single(sim.State.LiveOrders);
        sim.WaitSeconds(sim.Tuning.Flow.RefillSeconds + 0.1);
        Assert.Equal(2, sim.State.LiveOrders.Count);
        Assert.True(sim.State.Tick < sim.Tuning.Ticks(sim.Tuning.Flow.IntervalSeconds)); // well before the next scheduled one
    }

    [Fact]
    public void Letters_pile_up_to_ten_but_never_more()
    {
        // Long deadlines so nothing expires: the board just fills up.
        var sim = Sim(2, new Tuning { DeadlineSeconds = 1000, RibbonDeadlineSeconds = 1000, RushDeadlineSeconds = 990 });
        int most = 0;
        while (sim.State.Phase == RunPhase.Playing) { sim.Step(PlayerCommand.None); most = Math.Max(most, sim.State.LiveOrders.Count); }
        Assert.Equal(10, most);
    }

    [Fact]
    public void Later_rounds_send_letters_more_often()
    {
        var t = new Tuning();
        int Scheduled(int round) => t.SpawnTicksFor(GameMode.Standard, round).Count(x => x != long.MaxValue);
        Assert.True(Scheduled(3) > Scheduled(2));
        Assert.True(Scheduled(5) > Scheduled(3));
        Assert.True(t.SpawnTicksFor(GameMode.Relaxed, 2).Count(x => x != long.MaxValue) < Scheduled(2));
    }

    [Fact]
    public void Santa_stocks_kits_for_every_planned_letter_including_the_reserve()
    {
        var sim = Sim(2, supply: true);
        Assert.Equal(sim.State.SpawnTicks.Length, sim.State.OrderPlan.Count);
        Assert.Equal(sim.State.OrderPlan.Count(p => !p.Naughty), sim.State.KitStock.Values.Sum());
        Assert.Equal(sim.Tuning.Flow.ReserveLetters, sim.State.SpawnTicks.Count(x => x == long.MaxValue));
    }

    [Fact]
    public void A_rush_event_with_an_end_makes_every_order_in_the_window_rush()
    {
        var content = ContentLoader.Build(new Tuning(),
            TestContent.Create().Toys.ToList(), TestContent.Create().Stations.ToList(),
            [new SantaEvent { Id = "rush", Type = SantaEventType.Rush, WarnAt = 0, StartAt = 0, EndAt = 40 }],
            new DialogueBook(), []);
        var sim = new Simulation(content, GameMode.Standard);
        sim.WaitSeconds(60);
        var made = sim.State.LiveOrders.Concat(sim.State.ClosedOrders).ToList();
        Assert.All(made.Where(o => o.CreatedTick < sim.Tuning.Ticks(40)), o => Assert.True(o.IsRush));
        Assert.All(made.Where(o => o.CreatedTick >= sim.Tuning.Ticks(40)), o => Assert.False(o.IsRush));
        Assert.True(made.Count(o => o.IsRush) >= 3); // 0, 18 and 36 s
    }

    [Fact]
    public void A_rush_order_that_needs_a_ribbon_keeps_the_ribbon_time()
    {
        var t = new Tuning();
        var sim = new Simulation(TestContent.Create(), GameMode.Standard, round: 4); // a rush shift
        sim.WaitSeconds(70); // past a letter written in the first ribbon window (60 to 90 s)
        var ribbon = sim.State.LiveOrders.First(o => o.RequiresRibbon);
        Assert.True(ribbon.IsRush);
        Assert.Equal(t.Ticks((t.RibbonDeadlineSeconds - (t.DeadlineSeconds - t.RushDeadlineSeconds)) * t.DeadlineMultiplier(GameMode.Standard, 4)),
            ribbon.AllowedDurationTicks);
    }

    [Fact]
    public void Round_4_is_a_rush_shift_and_Santa_says_so()
    {
        var t = new Tuning();
        Assert.False(t.Rounds.IsRushShift(3));
        Assert.True(t.Rounds.IsRushShift(4));
        Assert.False(t.Rounds.IsRushShift(5));
        Assert.True(t.Rounds.IsRushShift(7));

        var sim = new Simulation(TestContent.Create(), GameMode.Standard, round: 4);
        var events = new List<SimEvent>();
        for (int i = 0; i < t.Ticks(30); i++) { sim.Step(PlayerCommand.None); events.AddRange(sim.DrainEvents()); }
        Assert.True(sim.State.RushShift);
        Assert.All(sim.State.LiveOrders.Concat(sim.State.ClosedOrders), o => Assert.True(o.IsRush));
        // Two notices: the rush shift, then the keys moving (round 4 is also where the parts change keys).
        Assert.Equal(2, events.Count(e => e.Type == SimEventType.PolicyStarted));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)] // parts go on in a new order from round 2
    [InlineData(3, 0)] // nothing new
    public void Santa_announces_an_assembly_policy_change_at_the_start_of_the_round(int round, int notices)
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard, round: round);
        var events = new List<SimEvent>();
        for (int i = 0; i < sim.Tuning.Ticks(10); i++) { sim.Step(PlayerCommand.None); events.AddRange(sim.DrainEvents()); }
        Assert.Equal(notices, events.Count(e => e.Type == SimEventType.PolicyStarted));
    }

    [Fact]
    public void The_bench_change_is_known_for_each_round_so_it_can_be_warned_about()
    {
        var a = new Tuning().Assembly;
        Assert.Null(a.PolicyChange(GameMode.Standard, 1));
        Assert.Equal("assembly_order", a.PolicyChange(GameMode.Standard, 2));
        Assert.Null(a.PolicyChange(GameMode.Standard, 3));
        Assert.Equal("assembly_keys", a.PolicyChange(GameMode.Standard, 4));
        Assert.Null(a.PolicyChange(GameMode.Standard, 5));
        Assert.Equal("assembly_order", a.PolicyChange(GameMode.Relaxed, 3)); // relaxed plays a round behind
    }
}
