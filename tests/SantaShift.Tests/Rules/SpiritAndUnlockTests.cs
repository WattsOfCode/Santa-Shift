using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Persistence;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>The Christmas Spirit meter (lasts a run) and unlocks (last across runs).</summary>
public class SpiritAndUnlockTests
{
    private static readonly SpiritTuning On = new() { Enabled = true, Start = 50, Max = 100, Delivered = 5, Missed = 20, Botched = 10, BetweenRounds = 15, JollyAt = 75, JollyBonus = 0.5 };

    private static Simulation NewSim(double? spirit = null) =>
        new(TestContent.Create(tuning: new Tuning { Spirit = On }), GameMode.Standard, spirit: spirit);

    [Fact]
    public void A_delivery_raises_the_spirit()
    {
        var sim = NewSim();
        sim.Wait(1);
        sim.MakeToy("train");
        sim.Use("shipping");
        Assert.Equal(55, sim.State.Spirit);
    }

    [Fact]
    public void A_botched_toy_and_a_missed_order_lower_it()
    {
        var sim = NewSim();
        sim.Wait(1);
        sim.Use("kits_train");
        sim.Use("bench");
        sim.PressPart(sim.State.Assembly!.NextKey % 3 + 1); // a wrong part
        Assert.Equal(40, sim.State.Spirit);

        sim.WaitSeconds(sim.Tuning.DeadlineSeconds + 1);  // the train order runs out
        Assert.True(sim.State.Spirit <= 20);
    }

    [Fact]
    public void When_the_spirit_runs_out_the_shift_ends_as_a_failure()
    {
        var sim = NewSim(spirit: 15);
        sim.Wait(1);
        sim.WaitSeconds(sim.Tuning.DeadlineSeconds + 1);
        Assert.Equal(RunPhase.Results, sim.State.Phase);
        Assert.True(sim.State.SpiritOut);
        var run = new RunProgress(GameMode.Standard);
        Assert.False(run.CompleteRound(sim.State, quota: 0)); // even with the quota met
    }

    [Fact]
    public void High_spirit_earns_jolly_bonus_points()
    {
        int Score(double spirit)
        {
            var sim = NewSim(spirit);
            sim.Wait(1);
            sim.MakeToy("train");
            sim.Use("shipping");
            return sim.State.Score;
        }
        Assert.Equal(Math.Round(Score(50) * 1.5), Score(90), 0);
    }

    [Fact]
    public void Without_the_meter_nothing_changes()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard, spirit: 1);
        sim.Wait(1);
        sim.WaitSeconds(sim.Tuning.DeadlineSeconds + 1);
        Assert.Equal(RunPhase.Playing, sim.State.Phase);
    }

    [Fact]
    public void Spirit_carries_into_the_next_round_with_a_little_rest()
    {
        var run = new RunProgress(GameMode.Standard);
        Assert.Equal(50, run.SpiritForNextRound(On));
        run.CompleteRound(new RunState { Delivered = 9, Spirit = 40 }, 9);
        Assert.Equal(55, run.SpiritForNextRound(On));
        run.StartNextRound();
        run.CompleteRound(new RunState { Delivered = 9, Spirit = 95 }, 9);
        Assert.Equal(100, run.SpiritForNextRound(On));
    }

    private static readonly List<Unlock> Unlocks =
    [
        new() { Id = "perk_crates", Stat = UnlockStat.ToysDelivered, Target = 5, Reward = UnlockReward.Perk, Value = "crates" },
        new() { Id = "hat_green", Stat = UnlockStat.CoalDelivered, Target = 2, Reward = UnlockReward.Hat, Value = "#2F8A52" },
        new() { Id = "head_start", Stat = UnlockStat.BestRound, Target = 3, Reward = UnlockReward.HeadStart },
    ];

    [Fact]
    public void Rounds_add_to_the_totals_and_earn_unlocks_once()
    {
        var p = new PlayerProgress();
        var first = p.RecordRound(new RunState { Delivered = 4, CoalDelivered = 1 }, round: 1, passed: true, Unlocks);
        Assert.Empty(first);
        Assert.Equal(3, p.ToysDelivered);
        Assert.Equal(2, p.BestRound); // reached round 2

        var second = p.RecordRound(new RunState { Delivered = 4, CoalDelivered = 1 }, round: 3, passed: false, Unlocks); // failed round 3
        Assert.Equal(["perk_crates", "hat_green", "head_start"], second.Select(u => u.Id));
        Assert.Equal(1, p.RunsPlayed);
        Assert.Empty(p.RecordRound(new RunState { Delivered = 9 }, 1, false, Unlocks)); // never twice
    }

    [Fact]
    public void A_perk_held_back_by_an_unlock_is_offered_once_earned()
    {
        var perks = new List<Perk> { new() { Id = "boots" }, new() { Id = "crates" } };
        var p = new PlayerProgress();
        Assert.Equal(["boots"], p.AvailablePerks(perks, Unlocks).Select(x => x.Id));
        Assert.False(p.HasHeadStart(Unlocks));
        p.Unlocked.AddRange(["perk_crates", "head_start"]);
        Assert.Equal(["boots", "crates"], p.AvailablePerks(perks, Unlocks).Select(x => x.Id));
        Assert.True(p.HasHeadStart(Unlocks));
    }

    [Fact]
    public void Progress_survives_a_save_and_a_hat_must_be_unlocked()
    {
        string dir = Path.Combine(Path.GetTempPath(), "santashift-progress-" + Guid.NewGuid().ToString("N"));
        try
        {
            var data = new SaveData(new JsonFileStore(dir));
            data.SaveProgress(new PlayerProgress { ToysDelivered = 12, Unlocked = ["hat_green"], Hat = "hat_green" });
            var loaded = data.LoadProgress();
            Assert.Equal(12, loaded.ToysDelivered);
            Assert.Equal("hat_green", loaded.Hat);

            data.SaveProgress(new PlayerProgress { Hat = "hat_gold" }); // not unlocked
            Assert.Equal("", data.LoadProgress().Hat);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Shipped_unlocks_point_at_real_perks()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        Assert.True(content.Unlocks.Count >= 5);
        Assert.True(content.Tuning.Spirit.Enabled);
        Assert.All(content.Unlocks.Where(u => u.Reward == UnlockReward.Perk), u => Assert.Contains(content.Perks, p => p.Id == u.Value));
        Assert.All(content.Unlocks.Where(u => u.Reward == UnlockReward.Hat), u => Assert.StartsWith("#", u.Value));
    }
}
