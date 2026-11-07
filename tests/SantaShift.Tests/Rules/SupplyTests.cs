using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>
/// The supply sled: with it in the room, each kit bin is filled once when the shift starts (one kit per toy the shift
/// will ask for). A wasted kit leaves a bin short, and the elf must fetch a supply crate from the sled to restock it.
/// </summary>
public class SupplyTests
{
    private static readonly List<ChildLetter> Letters =
    [
        new() { Id = "ann", Child = "Ann", Age = 6, ToyId = "train", Line = "Choo" },
        new() { Id = "ben", Child = "Ben", Age = 7, ToyId = "bear", Line = "Hug" },
    ];

    /// <summary>The first order (tick 0) is a train.</summary>
    private static Simulation NewSim(bool supply = true, params string[] naughty) =>
        new(TestContent.Create(Letters, supply: supply), GameMode.Standard, naughty: naughty);

    [Fact]
    public void Without_a_supply_sled_bins_never_run_out()
    {
        var sim = NewSim(supply: false);
        Assert.False(sim.State.LimitedKits);
        Assert.Null(sim.State.KitsLeft("train"));
        for (int i = 0; i < 3; i++)
        {
            Assert.Contains(sim.Use("kits_robot"), e => e.Type == SimEventType.Collected); // no robot order needed
            sim.State.CarriedItem = null;
        }
    }

    [Fact]
    public void Bins_start_with_one_kit_for_every_toy_the_shift_will_ask_for()
    {
        var sim = NewSim();
        Assert.True(sim.State.LimitedKits);
        // 15 scheduled orders cycling train, bear, robot: five of each.
        Assert.Equal(5, sim.State.KitsLeft("train"));
        Assert.Equal(5, sim.State.KitsLeft("bear"));
        Assert.Equal(5, sim.State.KitsLeft("robot"));
        Assert.Equal(sim.Tuning.SpawnTimes.Length, sim.State.OrderPlan.Count);
    }

    [Fact]
    public void Bins_are_not_topped_up_during_the_shift()
    {
        var sim = NewSim();
        int trains = sim.State.KitsLeft("train")!.Value;
        sim.WaitSeconds(100); // six orders arrive (three trains among them), nothing collected
        Assert.Equal(trains, sim.State.KitsLeft("train"));
        Assert.Equal(5, sim.State.KitsLeft("bear"));
    }

    [Fact]
    public void Orders_follow_the_plan_made_at_the_start()
    {
        var sim = NewSim();
        var plan = sim.State.OrderPlan.ToList();
        sim.WaitSeconds(40); // orders at 0, 18 and 36 s
        var made = sim.State.LiveOrders.OrderBy(o => o.Id).ToList();
        Assert.Equal(3, made.Count);
        for (int i = 0; i < made.Count; i++)
        {
            Assert.Equal(plan[i].ToyId, made[i].ToyId);
            Assert.Equal(plan[i].Naughty, made[i].Naughty);
            Assert.Same(plan[i].Letter, made[i].Letter);
        }
    }

    [Fact]
    public void Collecting_takes_a_kit_and_an_empty_bin_refuses()
    {
        var sim = NewSim();
        sim.State.KitStock["train"] = 1;
        sim.Wait(1);
        Assert.Contains(sim.Use("kits_train"), e => e.Type == SimEventType.Collected);
        Assert.Equal(0, sim.State.KitsLeft("train"));

        sim.State.CarriedItem = null; // say the kit got botched and scrapped
        var events = sim.Use("kits_train");
        Assert.Contains(events, e => e.Type == SimEventType.Rejected && e.Text == "bin_empty");
        Assert.Null(sim.State.CarriedItem);
        Assert.Equal("bin_empty", sim.Prompt());
    }

    [Fact]
    public void A_naughty_childs_letter_sends_no_kit()
    {
        var sim = NewSim(naughty: "Ann"); // Ann wrote every train letter here
        Assert.Equal(0, sim.State.KitsLeft("train"));
        sim.Wait(1);
        Assert.True(sim.State.LiveOrders.Single().Naughty);
        Assert.Equal(0, sim.State.KitsLeft("train"));
    }

    [Fact]
    public void A_supply_crate_from_the_sled_restocks_a_bin()
    {
        var sim = NewSim();
        sim.State.KitStock["bear"] = 0;
        Assert.Equal("prompt_supply", PromptAt(sim, "supply"));
        Assert.Contains(sim.Use("supply"), e => e.Type == SimEventType.SupplyTaken);
        Assert.True(sim.State.CarriedItem!.IsSupply);
        Assert.Equal("item_supply", sim.ItemName(sim.State.CarriedItem));

        Assert.Equal("prompt_restock", PromptAt(sim, "kits_bear"));
        var events = sim.Use("kits_bear");
        Assert.Contains(events, e => e.Type == SimEventType.Restocked && e.ToyId == "bear");
        Assert.Null(sim.State.CarriedItem);
        Assert.Equal(sim.Tuning.ResupplyKits, sim.State.KitsLeft("bear"));
        Assert.Contains(sim.Use("kits_bear"), e => e.Type == SimEventType.Collected);
        Assert.Equal(sim.Tuning.ResupplyKits - 1, sim.State.KitsLeft("bear"));
    }

    [Fact]
    public void The_sled_wants_empty_hands()
    {
        var sim = NewSim();
        sim.Use("supply");
        var events = sim.Use("supply");
        Assert.Contains(events, e => e.Type == SimEventType.Rejected && e.Text == "hands_full");
        Assert.True(sim.State.CarriedItem!.IsSupply);
    }

    [Theory]
    [InlineData("bench")]
    [InlineData("ribbon")]
    [InlineData("shipping")]
    public void A_supply_crate_only_goes_in_a_kit_bin(string station)
    {
        var sim = NewSim();
        sim.Wait(1);
        sim.Use("supply");
        Assert.Equal("supply_goes_to_bin", PromptAt(sim, station));
        var events = sim.Use(station);
        Assert.Contains(events, e => e.Type == SimEventType.Rejected && e.Text == "supply_goes_to_bin");
        Assert.True(sim.State.CarriedItem!.IsSupply);
        Assert.Equal(0, sim.State.Delivered);
    }

    [Fact]
    public void A_supply_crate_can_be_dropped_in_the_barrel_without_asking()
    {
        var sim = NewSim();
        sim.Use("supply");
        Assert.Contains(sim.Use("recycle"), e => e.Type == SimEventType.Recycled);
        Assert.Null(sim.State.CarriedItem);
    }

    [Fact]
    public void A_full_round_of_toys_needs_no_resupply_when_nothing_is_wasted()
    {
        var sim = NewSim();
        int kits = 0;
        // Deliver every order the shift creates, straight away: no bin should ever come up short.
        while (sim.State.Phase == RunPhase.Playing)
        {
            sim.Wait(1);
            foreach (var order in sim.State.LiveOrders.Where(o => !o.Naughty).ToList())
            {
                Assert.True(sim.State.KitsLeft(order.ToyId) > 0, $"{order.ToyId} bin ran out at tick {sim.State.Tick}");
                sim.MakeToy(order.ToyId, order.RequiresRibbon);
                if (order.RequiresRibbon && order.RibbonColor != null) sim.State.CarriedItem!.RibbonColor = order.RibbonColor;
                sim.Use("shipping");
                kits++;
            }
            sim.State.LiveOrders.RemoveAll(o => o.Naughty); // coal is tested elsewhere
        }
        Assert.Equal(sim.Tuning.SpawnTimes.Length, kits);
        Assert.All(sim.State.KitStock.Values, v => Assert.Equal(0, v));
    }

    private static string? PromptAt(Simulation sim, string stationId)
    {
        sim.PlacePlayer(sim.StationById(stationId).Interact);
        return sim.Prompt();
    }
}
