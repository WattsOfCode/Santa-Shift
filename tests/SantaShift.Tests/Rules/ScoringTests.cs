using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>The score formula, using the GDD's own worked examples (section 24).</summary>
public class ScoringTests
{
    private static readonly Tuning T = new();

    private static Order Order(double allowedSeconds, bool ribbon = false, bool rush = false) => new()
    {
        Id = 1, ToyId = "train", RequiresRibbon = ribbon, IsRush = rush,
        CreatedTick = 0, DeadlineTick = T.Ticks(allowedSeconds), AllowedDurationTicks = T.Ticks(allowedSeconds),
    };

    [Fact]
    public void Gdd_example_first_ordinary_delivery_is_110()
    {
        var o = Order(55);
        long tick = o.DeadlineTick - T.Ticks(27.5);
        Assert.Equal(110, Scoring.Points(o, tick, streak: 1, T.Score));
    }

    [Fact]
    public void Gdd_example_second_ribbon_rush_delivery_is_165()
    {
        var o = Order(45, ribbon: true, rush: true);
        long tick = o.DeadlineTick - T.Ticks(22.5);
        Assert.Equal(165, Scoring.Points(o, tick, streak: 2, T.Score)); // 100 + 25 + 25 + 10 + 5
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 5)]
    [InlineData(3, 10)]
    [InlineData(4, 15)]
    [InlineData(5, 20)]
    [InlineData(12, 20)]
    public void Streak_bonus_is_capped_at_20(int streak, int expectedBonus)
    {
        var o = Order(55);
        Assert.Equal(100 + expectedBonus, Scoring.Points(o, o.DeadlineTick, streak, T.Score));
    }

    [Fact]
    public void Time_bonus_is_20_at_creation_and_0_at_the_deadline()
    {
        var o = Order(55);
        Assert.Equal(20, Scoring.TimeBonus(o, 0, T.Score));
        Assert.Equal(0, Scoring.TimeBonus(o, o.DeadlineTick, T.Score));
        Assert.Equal(0, Scoring.TimeBonus(o, o.DeadlineTick + 100, T.Score));
    }

    [Theory]
    [InlineData(0, "review_low")]
    [InlineData(8, "review_low")]
    [InlineData(9, "review_survivor")]
    [InlineData(11, "review_survivor")]
    [InlineData(12, "review_legend")]
    public void Review_label_depends_only_on_delivered_count(int delivered, string key)
    {
        Assert.Equal(key, Scoring.ReviewKey(delivered, T));
    }

    [Fact]
    public void Missed_orders_never_subtract_points()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.Wait(1);
        sim.MakeToy("train");
        sim.Use("shipping");
        int score = sim.State.Score;

        sim.WaitSeconds(120); // let later orders expire

        Assert.True(sim.State.Missed > 0);
        Assert.Equal(score, sim.State.Score);
    }

    [Fact]
    public void Best_score_is_kept_per_mode()
    {
        var best = new PersonalBest();
        Assert.True(best.Submit(GameMode.Standard, 500));
        Assert.False(best.Submit(GameMode.Standard, 400));
        Assert.True(best.Submit(GameMode.Relaxed, 300));

        Assert.Equal(500, best.For(GameMode.Standard));
        Assert.Equal(300, best.For(GameMode.Relaxed));
    }
}
