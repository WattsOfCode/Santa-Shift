using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>Perks: Santa offers a few after each passed round; picked ones change the rest of the run.</summary>
public class PerkTests
{
    private static Perk P(string id, PerkEffect e, double amount, int max = 3) => new() { Id = id, Name = id, Effect = e, Amount = amount, MaxStacks = max };

    private static readonly List<Perk> Pool =
    [
        P("boots", PerkEffect.WalkSpeed, 0.5), P("hammer", PerkEffect.BenchTime, 1), P("fingers", PerkEffect.RibbonSpeed, 0.5, 1),
        P("shovel", PerkEffect.ShovelSpeed, 0.5), P("crates", PerkEffect.SupplyKits, 1), P("reindeer", PerkEffect.Deadlines, 0.5),
        P("spirit", PerkEffect.ScoreBonus, 0.5),
    ];

    private static RunProgress PassedRun(int delivered, int quota = 9)
    {
        var run = new RunProgress(GameMode.Standard);
        run.CompleteRound(new RunState { Delivered = delivered }, quota);
        return run;
    }

    [Fact]
    public void Offers_are_different_perks_and_repeat_for_the_same_seed()
    {
        var run = PassedRun(9);
        var offers = run.OfferPerks(Pool, 3, 2, seed: 4);
        Assert.Equal(3, offers.Count);
        Assert.Equal(3, offers.Select(p => p.Id).Distinct().Count());
        Assert.Equal(offers.Select(p => p.Id), run.OfferPerks(Pool, 3, 2, seed: 4).Select(p => p.Id));
    }

    [Fact]
    public void A_round_well_over_quota_earns_an_extra_card()
    {
        Assert.Equal(3, PassedRun(10).OfferPerks(Pool, 3, 2, 1).Count);
        Assert.Equal(4, PassedRun(11).OfferPerks(Pool, 3, 2, 1).Count);
    }

    [Fact]
    public void A_perk_at_its_limit_is_no_longer_offered()
    {
        var run = PassedRun(9);
        run.AddPerk(Pool.Single(p => p.Id == "fingers")); // max 1
        for (int seed = 0; seed < 20; seed++)
            Assert.DoesNotContain(run.OfferPerks(Pool, 3, 2, seed), p => p.Id == "fingers");
        Assert.Throws<InvalidOperationException>(() => run.AddPerk(Pool.Single(p => p.Id == "fingers")));
    }

    [Fact]
    public void Perks_stack_into_the_run_modifiers()
    {
        var run = PassedRun(9);
        run.AddPerk(Pool[0]);
        run.AddPerk(Pool[0]);
        run.AddPerk(Pool[1]);
        var m = run.Modifiers;
        Assert.Equal(2.0, m.SpeedMultiplier, 3);
        Assert.Equal(1.0, m.BenchSeconds, 3);
        Assert.Equal(1.0, RunModifiers.None.SpeedMultiplier);
    }

    private static Simulation SimWith(params string[] perks) =>
        new(TestContent.Create(supply: true), GameMode.Standard, modifiers: RunModifiers.From(perks.Select(id => Pool.Single(p => p.Id == id))));

    [Fact]
    public void Faster_boots_walk_further()
    {
        double Walk(Simulation sim)
        {
            sim.PlacePlayer(new Point2(480, 400));
            for (int i = 0; i < 30; i++) sim.Step(new PlayerCommand(1, 0, false));
            return sim.State.PlayerPosition.X - 480;
        }
        Assert.Equal(Walk(SimWith()) * 1.5, Walk(SimWith("boots")), 1);
    }

    [Fact]
    public void Steady_hammer_gives_more_time_at_the_bench()
    {
        long Limit(Simulation sim)
        {
            sim.Wait(1);
            sim.Use("kits_train");
            sim.Use("bench");
            return sim.State.Assembly!.DeadlineTick - sim.State.Assembly.StartTick;
        }
        Assert.Equal(Limit(SimWith()) + 60, Limit(SimWith("hammer")));
    }

    [Fact]
    public void Bigger_crates_restock_more_kits()
    {
        var sim = SimWith("crates");
        int before = sim.State.KitsLeft("bear")!.Value;
        sim.Use("supply");
        sim.Use("kits_bear");
        Assert.Equal(before + sim.Tuning.ResupplyKits + 1, sim.State.KitsLeft("bear"));
    }

    [Fact]
    public void Express_reindeer_lengthen_deadlines_and_spirit_raises_points()
    {
        var plain = SimWith();
        var boosted = SimWith("reindeer", "spirit");
        plain.Wait(1);
        boosted.Wait(1);
        var a = plain.State.LiveOrders.Single();
        var b = boosted.State.LiveOrders.Single();
        Assert.Equal(a.AllowedDurationTicks * 1.5, b.AllowedDurationTicks, 0);

        plain.MakeToy("train"); plain.Use("shipping");
        boosted.MakeToy("train"); boosted.Use("shipping");
        Assert.True(boosted.State.Score > plain.State.Score * 1.4);
    }

    [Fact]
    public void Shipped_perks_load_cleanly()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        Assert.True(content.Perks.Count >= 5);
        Assert.Empty(content.Warnings);
        Assert.Equal(3, content.Tuning.RibbonColors.Length);
    }
}
