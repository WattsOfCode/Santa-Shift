using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>Ribbon colours: the elf picks a spool at the ribbon station, and coloured orders want that colour.</summary>
public class RibbonColourTests
{
    private static readonly RibbonColor[] Colours =
    [
        new() { Id = "red", Name = "Red" }, new() { Id = "gold", Name = "Gold" }, new() { Id = "green", Name = "Green" },
    ];

    private static Simulation NewSim() => new(TestContent.Create(tuning: new Tuning { RibbonColors = Colours }), GameMode.Standard);

    /// <summary>A train, built and carried to the ribbon station.</summary>
    private static Simulation WithTrainAtRibbon()
    {
        var sim = NewSim();
        sim.MakeToy("train");
        sim.Use("ribbon");
        return sim;
    }

    private static void AddRibbonOrder(Simulation sim, string? colour) => sim.State.LiveOrders.Add(new Order
    {
        Id = sim.State.NextId(), ToyId = "train", RequiresRibbon = true, RibbonColor = colour,
        CreatedTick = sim.State.Tick, DeadlineTick = sim.State.Tick + 6000, AllowedDurationTicks = 6000,
    });

    [Fact]
    public void Using_the_ribbon_station_first_asks_for_a_colour()
    {
        var sim = WithTrainAtRibbon();
        Assert.NotNull(sim.State.RibbonPick);
        Assert.Null(sim.State.CraftingAction);
        Assert.True(sim.State.IsWorking);
        Assert.Equal("picking_ribbon", sim.Prompt());
    }

    [Fact]
    public void Pressing_a_spools_key_ties_that_colour()
    {
        var sim = WithTrainAtRibbon();
        var events = sim.PressPart(2);
        Assert.Contains(events, e => e.Type == SimEventType.RibbonStarted && e.Text == "Gold");
        Assert.Null(sim.State.RibbonPick);
        sim.FinishCrafting();
        Assert.True(sim.State.CarriedItem!.HasRibbon);
        Assert.Equal("gold", sim.State.CarriedItem.RibbonColor);
    }

    [Fact]
    public void Moving_away_cancels_the_choice_and_keeps_the_toy()
    {
        var sim = WithTrainAtRibbon();
        sim.Step(new PlayerCommand(1, 0, false));
        Assert.Null(sim.State.RibbonPick);
        Assert.False(sim.State.CarriedItem!.HasRibbon);
    }

    [Fact]
    public void A_key_with_no_spool_does_nothing()
    {
        var sim = WithTrainAtRibbon();
        sim.PressPart(7);
        Assert.NotNull(sim.State.RibbonPick);
    }

    [Fact]
    public void A_coloured_order_takes_only_its_colour()
    {
        var sim = WithTrainAtRibbon();
        sim.PressPart(1); // red
        sim.FinishCrafting();
        sim.State.LiveOrders.Clear(); // the opening plain train order would take any train
        AddRibbonOrder(sim, "green");

        var events = sim.Use("shipping");
        Assert.Contains(events, e => e.Type == SimEventType.Rejected && e.Text == "wrong_ribbon_colour");
        Assert.NotNull(sim.State.CarriedItem);

        AddRibbonOrder(sim, "red");
        Assert.Contains(sim.Use("shipping"), e => e.Type == SimEventType.Delivered);
    }

    [Fact]
    public void An_order_with_no_colour_takes_any_ribbon()
    {
        var sim = WithTrainAtRibbon();
        sim.PressPart(3);
        sim.FinishCrafting();
        sim.State.LiveOrders.Clear();
        AddRibbonOrder(sim, null);
        Assert.Contains(sim.Use("shipping"), e => e.Type == SimEventType.Delivered);
    }

    [Fact]
    public void Ribbon_orders_ask_for_a_colour_from_the_list()
    {
        var sim = NewSim();
        sim.WaitSeconds(61); // the first ribbon window opens at 60 s
        sim.WaitSeconds(18);
        var ribboned = sim.State.LiveOrders.Concat(sim.State.ClosedOrders).Where(o => o.RequiresRibbon).ToList();
        Assert.NotEmpty(ribboned);
        Assert.All(ribboned, o => Assert.Contains(Colours, c => c.Id == o.RibbonColor));
    }

    [Fact]
    public void Without_colours_the_ribbon_is_tied_at_once()
    {
        var sim = new Simulation(TestContent.Create(), GameMode.Standard);
        sim.MakeToy("train");
        sim.Use("ribbon");
        Assert.Null(sim.State.RibbonPick);
        Assert.NotNull(sim.State.CraftingAction);
    }

    [Fact]
    public void Pausing_drops_the_choice()
    {
        var sim = WithTrainAtRibbon();
        sim.Pause();
        Assert.Null(sim.State.RibbonPick);
        Assert.NotNull(sim.State.CarriedItem);
    }
}
