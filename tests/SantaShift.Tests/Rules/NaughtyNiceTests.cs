using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>Santa's naughty and nice list: naughty children's orders want coal from the fireplace instead of a toy.</summary>
public class NaughtyNiceTests
{
    private static readonly List<ChildLetter> Letters =
    [
        new() { Id = "ann", Child = "Ann", Age = 6, ToyId = "train", Line = "Choo" },
        new() { Id = "ben", Child = "Ben", Age = 7, ToyId = "bear", Line = "Hug" },
        new() { Id = "cat", Child = "Cat", Age = 8, ToyId = "robot", Line = "Beep" },
        new() { Id = "dan", Child = "Dan", Age = 5, ToyId = "train", Line = "Toot" },
    ];

    /// <summary>The first order (tick 0) is a train for Ann.</summary>
    private static Simulation NewSim(params string[] naughty) =>
        new(TestContent.Create(Letters), GameMode.Standard, naughty: naughty);

    [Fact]
    public void The_list_names_every_child_once_and_marks_the_requested_number_naughty()
    {
        var list = NaughtyNiceList.Draw(Letters, 2, seed: 5);
        Assert.Equal(["Ann", "Ben", "Cat", "Dan"], list.Entries.Select(e => e.Child));
        Assert.Equal(2, list.NaughtyChildren.Count);
        Assert.Equal(2, list.Entries.Count(e => e.Naughty));
    }

    [Fact]
    public void The_same_seed_gives_the_same_list_and_seeds_vary_it()
    {
        Assert.Equal(NaughtyNiceList.Draw(Letters, 1, 9).NaughtyChildren, NaughtyNiceList.Draw(Letters, 1, 9).NaughtyChildren);
        var picks = Enumerable.Range(0, 30).Select(s => NaughtyNiceList.Draw(Letters, 1, s).NaughtyChildren.Single()).ToHashSet();
        Assert.True(picks.Count > 1);
    }

    [Fact]
    public void A_new_run_draws_its_list_from_the_letters()
    {
        var run = RunProgress.Start(GameMode.Standard, Letters, 3, seed: 1);
        Assert.Equal(3, run.List.NaughtyChildren.Count);
        Assert.Equal(4, run.List.Entries.Count);
    }

    [Fact]
    public void A_naughty_childs_order_is_marked_and_never_needs_a_ribbon()
    {
        var sim = NewSim("Ann");
        sim.Wait(1);
        var order = Assert.Single(sim.State.LiveOrders);
        Assert.True(order.Naughty);
        Assert.Equal("Ann", order.Letter!.Child);
        Assert.False(order.RequiresRibbon);
    }

    [Fact]
    public void Coal_from_the_fireplace_ships_to_the_naughty_child_and_scores()
    {
        var sim = NewSim("Ann");
        sim.Wait(1);
        Assert.Contains(sim.Use("coal"), e => e.Type == SimEventType.ShovelStarted);
        sim.FinishCrafting();
        Assert.True(sim.State.CarriedItem!.IsCoal);

        var events = sim.Use("shipping");

        Assert.Equal(1, sim.State.Delivered);
        Assert.True(sim.State.Score > 0);
        Assert.Null(sim.State.CarriedItem);
        Assert.Contains(events, e => e.Type == SimEventType.Delivered && e.Text == "coal_delivered");
    }

    [Fact]
    public void A_naughty_child_does_not_get_the_toy()
    {
        var sim = NewSim("Ann");
        sim.Wait(1);
        sim.MakeToy("train");

        var events = sim.Use("shipping");

        Assert.Equal(0, sim.State.Delivered);
        Assert.NotNull(sim.State.CarriedItem);
        Assert.Contains(events, e => e.Type == SimEventType.Rejected && e.Text == "naughty_no_toy");
    }

    [Fact]
    public void Nice_children_still_get_toys_and_coal_is_refused_for_them()
    {
        var sim = NewSim();
        sim.Wait(1);
        sim.Use("coal");
        sim.FinishCrafting();
        Assert.Contains(sim.Use("shipping"), e => e.Text == "coal_no_naughty");
        Assert.Equal(0, sim.State.Delivered);

        Assert.Contains(sim.Use("recycle"), e => e.Type == SimEventType.Recycled); // coal goes straight in
        sim.MakeToy("train");
        sim.Use("shipping");
        Assert.Equal(1, sim.State.Delivered);
    }

    [Fact]
    public void Coal_cannot_be_built_or_ribboned_and_the_scuttle_needs_empty_hands()
    {
        var sim = NewSim("Ann");
        sim.Use("coal");
        sim.FinishCrafting();
        Assert.Contains(sim.Use("bench"), e => e.Text == "coal_not_a_kit");
        Assert.Contains(sim.Use("ribbon"), e => e.Text == "no_ribbon_for_coal");
        Assert.Contains(sim.Use("coal"), e => e.Text == "hands_full");
        Assert.Null(sim.State.Assembly);
    }

    [Fact]
    public void Shovelling_coal_takes_the_shovel_time_and_moving_stops_it()
    {
        var sim = NewSim("Ann");
        sim.Use("coal");
        sim.Wait(sim.Tuning.Ticks(sim.Tuning.ShovelSeconds) - 2);
        Assert.Null(sim.State.CarriedItem);

        sim.Step(new PlayerCommand(1, 0, false));
        Assert.Null(sim.State.CraftingAction);
        Assert.Null(sim.State.CarriedItem);
        Assert.Contains(sim.DrainEvents(), e => e.Text == "shovel_cancelled");

        sim.Use("coal");
        var events = new List<SimEvent>();
        for (long i = 0; i < sim.Tuning.Ticks(sim.Tuning.ShovelSeconds); i++) { sim.Step(PlayerCommand.None); events.AddRange(sim.DrainEvents()); }
        Assert.True(sim.State.CarriedItem!.IsCoal);
        Assert.Contains(events, e => e.Type == SimEventType.CoalCollected);
    }

    [Fact]
    public void Without_a_coal_station_nobody_is_treated_as_naughty()
    {
        var full = TestContent.Create(Letters);
        var noCoal = ContentLoader.Build(new Tuning(), full.Toys.ToList(),
            full.Stations.Where(s => s.Type != StationType.Coal).ToList(), full.Events.ToList(), new DialogueBook(), Letters);
        var sim = new Simulation(noCoal, GameMode.Standard, naughty: ["Ann"]);
        sim.Wait(1);
        Assert.False(Assert.Single(sim.State.LiveOrders).Naughty);
    }
}
