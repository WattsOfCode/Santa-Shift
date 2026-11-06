using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>Rounds: meet the quota to continue (each round harder), miss it and the run is over.</summary>
public class RoundTests
{
    private static readonly Tuning T = new();

    [Fact]
    public void Quota_rises_by_one_each_round()
    {
        Assert.Equal(9, T.QuotaFor(GameMode.Standard, 1));
        Assert.Equal(10, T.QuotaFor(GameMode.Standard, 2));
        Assert.Equal(13, T.QuotaFor(GameMode.Standard, 5));
        Assert.Equal(7, T.QuotaFor(GameMode.Relaxed, 2));
    }

    [Fact]
    public void Deadlines_shrink_each_round_but_never_below_the_floor()
    {
        Assert.Equal(1.0, T.DeadlineMultiplier(GameMode.Standard, 1), 6);
        Assert.Equal(0.95, T.DeadlineMultiplier(GameMode.Standard, 2), 6);
        Assert.Equal(0.7, T.DeadlineMultiplier(GameMode.Standard, 50), 6);
        Assert.Equal(1.5 * 0.95, T.DeadlineMultiplier(GameMode.Relaxed, 2), 6);
    }

    [Fact]
    public void A_later_round_creates_orders_with_shorter_deadlines()
    {
        var round1 = new Simulation(TestContent.Create(), GameMode.Standard, round: 1);
        var round3 = new Simulation(TestContent.Create(), GameMode.Standard, round: 3);
        round1.Wait(1);
        round3.Wait(1);

        Assert.Equal(3, round3.State.Round);
        Assert.Equal(11, round3.Quota);
        Assert.True(round3.State.LiveOrders.Single().AllowedDurationTicks < round1.State.LiveOrders.Single().AllowedDurationTicks);
        Assert.Equal(T.Ticks(55 * 0.95 * 0.95), round3.State.LiveOrders.Single().AllowedDurationTicks);
    }

    [Fact]
    public void Meeting_the_quota_continues_the_run_and_carries_the_score()
    {
        var run = new RunProgress(GameMode.Standard);
        var round = new RunState { Delivered = 9, Score = 1200 };

        Assert.True(run.CompleteRound(round, quota: 9));
        Assert.False(run.IsOver);
        run.StartNextRound();

        Assert.Equal(2, run.Round);
        Assert.Equal(1200, run.TotalScore);
        Assert.Equal(1, run.RoundsCleared);
    }

    [Fact]
    public void Missing_the_quota_is_game_over()
    {
        var run = new RunProgress(GameMode.Standard);
        run.CompleteRound(new RunState { Delivered = 9, Score = 1000 }, 9);
        run.StartNextRound();

        bool passed = run.CompleteRound(new RunState { Delivered = 6, Score = 700 }, 10);

        Assert.False(passed);
        Assert.True(run.IsOver);
        Assert.Equal(2, run.Round);
        Assert.Equal(1700, run.TotalScore);
        Assert.Throws<InvalidOperationException>(run.StartNextRound);
    }

    [Fact]
    public void Best_records_the_furthest_round_separately_from_score()
    {
        var best = new PersonalBest();
        best.Submit(GameMode.Standard, 3000, round: 4);
        best.Submit(GameMode.Standard, 2000, round: 6); // lower score, further round

        Assert.Equal(3000, best.For(GameMode.Standard));
        Assert.Equal(6, best.RoundFor(GameMode.Standard));
    }
}
