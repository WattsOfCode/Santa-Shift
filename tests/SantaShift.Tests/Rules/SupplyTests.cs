using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>
/// The supply sled: with it in the room, each kit bin only holds the kits Santa sent for the letters so far (one per toy
/// asked for). A wasted kit leaves a bin short, and the elf must fetch a supply crate from the sled to restock it.
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
    public void Bins_start_empty_and_each_letter_sends_one_kit_for_its_toy()
    {
        var sim = NewSim();
        Assert.True(sim.State.LimitedKits);
        Assert.Equal(0, sim.State.KitsLeft("train"));

        sim.Wait(1); // the first order: a train
        Assert.Equal(1, sim.State.KitsLeft("train"));
        Assert.Equal(0, sim.State.KitsLeft("bear"));
    }

    [Fact]
    public void Collecting_takes_a_kit_and_an_empty_bin_refuses()
    {
        var sim = NewSim();
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
        var sim = NewSim(naughty: "Ann");
        sim.Wait(1);
        Assert.True(sim.State.LiveOrders.Single().Naughty);
        Assert.Equal(0, sim.State.KitsLeft("train"));
    }

    [Fact]
    public void A_supply_crate_from_the_sled_restocks_a_bin()
    {
        var sim = NewSim();
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
        sim.Wait(1);
        sim.MakeToy("train");
        sim.Use("shipping");
        Assert.Equal(1, sim.State.Delivered);
        Assert.Equal(0, sim.State.KitsLeft("train"));
    }

    private static string? PromptAt(Simulation sim, string stationId)
    {
        sim.PlacePlayer(sim.StationById(stationId).Interact);
        return sim.Prompt();
    }
}
