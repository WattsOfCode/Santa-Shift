using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>Item ownership and crafting (GDD sections 8 and 24).</summary>
public class CraftingTests
{
    private static Simulation NewSim() => new(TestContent.Create(), GameMode.Standard);

    [Fact]
    public void Dispenser_gives_a_kit_of_its_toy()
    {
        var sim = NewSim();
        var events = sim.Use("kits_bear");

        Assert.Equal("bear", sim.State.CarriedItem!.ToyId);
        Assert.Equal(ItemStage.Kit, sim.State.CarriedItem.Stage);
        Assert.False(sim.State.CarriedItem.HasRibbon);
        Assert.Contains(events, e => e.Type == SimEventType.Collected);
    }

    [Fact]
    public void Full_hands_at_a_dispenser_leaves_the_item_unchanged()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        var before = sim.State.CarriedItem;
        int revision = before!.Revision;

        var events = sim.Use("kits_bear");

        Assert.Same(before, sim.State.CarriedItem);
        Assert.Equal("train", sim.State.CarriedItem!.ToyId);
        Assert.Equal(revision, sim.State.CarriedItem.Revision);
        Assert.Contains(events, e => e.Type == SimEventType.Rejected && e.Text == "hands_full");
    }

    [Fact]
    public void Round_one_builds_parts_in_their_natural_order_on_keys_one_two_three()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        var item = sim.State.CarriedItem!;
        sim.Use("bench");

        var game = sim.State.Assembly!;
        Assert.Equal(["wheels", "boiler", "cab"], game.Order);
        Assert.Equal(["wheels", "boiler", "cab"], game.Keys);

        Assert.Contains(sim.PressPart(1), e => e.Type == SimEventType.PartAdded && e.Text == "wheels");
        Assert.Equal(1, item.PartsPlaced);
        sim.PressPart(2);
        Assert.Equal(ItemStage.Kit, item.Stage);

        var events = sim.PressPart(3);
        Assert.Contains(events, e => e.Type == SimEventType.AssemblyComplete);
        Assert.Equal(ItemStage.Assembled, item.Stage);
        Assert.Same(item, sim.State.CarriedItem);
        Assert.Null(sim.State.Assembly);
    }

    [Fact]
    public void A_part_out_of_order_botches_the_toy()
    {
        var sim = NewSim();
        sim.Use("kits_bear");
        sim.Use("bench");
        sim.PressPart(1);

        var events = sim.PressPart(3); // head before arms

        Assert.Equal(ItemStage.Botched, sim.State.CarriedItem!.Stage);
        Assert.Null(sim.State.Assembly);
        Assert.Contains(events, e => e.Type == SimEventType.AssemblyBotched && e.Text == "botched_wrong_part");
    }

    [Fact]
    public void Running_out_of_time_botches_the_toy()
    {
        var sim = NewSim();
        sim.Use("kits_robot");
        sim.Use("bench");
        sim.PressPart(1);

        sim.Wait(sim.Tuning.Ticks(sim.Tuning.Assembly.TimeLimitSeconds) - 2);
        Assert.NotNull(sim.State.Assembly);
        sim.Wait(1);

        Assert.Null(sim.State.Assembly);
        Assert.Equal(ItemStage.Botched, sim.State.CarriedItem!.Stage);
    }

    [Fact]
    public void Botched_toys_cannot_be_ribboned_shipped_or_rebuilt_but_go_straight_into_the_scrap_barrel()
    {
        var sim = NewSim();
        sim.Wait(1); // first order: train
        sim.Use("kits_train");
        sim.Use("bench");
        sim.PressPart(3);
        Assert.Equal(ItemStage.Botched, sim.State.CarriedItem!.Stage);

        Assert.Contains(sim.Use("bench"), e => e.Text == "botched_scrap_it");
        Assert.Contains(sim.Use("ribbon"), e => e.Text == "botched_scrap_it");
        Assert.Contains(sim.Use("shipping"), e => e.Text == "botched_scrap_it");
        Assert.Equal(0, sim.State.Delivered);

        var events = sim.Use("recycle"); // no confirmation needed
        Assert.Null(sim.State.CarriedItem);
        Assert.Contains(events, e => e.Type == SimEventType.Recycled);
    }

    [Fact]
    public void Moving_cancels_assembly_and_keeps_a_plain_kit()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        sim.Use("bench");
        sim.PressPart(1);

        sim.Step(new PlayerCommand(1, 0, false));
        var events = sim.DrainEvents();

        Assert.Null(sim.State.Assembly);
        Assert.Equal(ItemStage.Kit, sim.State.CarriedItem!.Stage);
        Assert.Equal(0, sim.State.CarriedItem.PartsPlaced);
        Assert.Contains(events, e => e.Type == SimEventType.ActionCancelled && e.Text == "assembly_cancelled");
    }

    [Fact]
    public void Pausing_freezes_the_minigame_clock()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        sim.Use("bench");
        var game = sim.State.Assembly!;

        sim.Pause();
        sim.Wait(10_000); // nothing happens while paused
        sim.Resume();

        Assert.Same(game, sim.State.Assembly);
        Assert.Equal(ItemStage.Kit, sim.State.CarriedItem!.Stage);
        Assert.DoesNotContain(sim.DrainEvents(), e => e.Type == SimEventType.ActionCancelled);
    }

    [Fact]
    public void Number_keys_do_nothing_away_from_the_bench()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        var events = sim.PressPart(1);
        Assert.Empty(events);
        Assert.Equal(0, sim.State.CarriedItem!.PartsPlaced);
    }

    [Fact]
    public void Bench_rejects_empty_hands_and_finished_toys()
    {
        var sim = NewSim();
        Assert.Contains(sim.Use("bench"), e => e.Text == "bench_needs_kit");

        sim.MakeToy("train");
        Assert.Contains(sim.Use("bench"), e => e.Text == "already_assembled");
        Assert.Null(sim.State.CraftingAction);
    }

    [Fact]
    public void Ribbon_station_needs_an_assembled_toy_without_a_ribbon()
    {
        var sim = NewSim();
        Assert.Contains(sim.Use("ribbon"), e => e.Text == "ribbon_needs_toy");

        sim.Use("kits_robot");
        Assert.Contains(sim.Use("ribbon"), e => e.Text == "assemble_first");

        sim.Use("bench");
        sim.FinishCrafting();
        sim.Use("ribbon");
        sim.Wait(sim.Tuning.Ticks(1.0));
        Assert.True(sim.State.CarriedItem!.HasRibbon);

        Assert.Contains(sim.Use("ribbon"), e => e.Text == "already_ribboned");
    }

    [Fact]
    public void Extra_presses_during_an_action_do_nothing()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        sim.Use("bench");
        var game = sim.State.Assembly;

        var events = sim.Use("bench");

        Assert.Same(game, sim.State.Assembly);
        Assert.DoesNotContain(events, e => e.Type == SimEventType.Rejected);
    }

    [Fact]
    public void Recycling_needs_confirmation_and_movement_cancels_it()
    {
        var sim = NewSim();
        Assert.Contains(sim.Use("recycle"), e => e.Text == "nothing_to_recycle");

        sim.Use("kits_train");
        sim.Use("recycle");
        Assert.True(sim.State.RecyclePromptOpen);
        Assert.NotNull(sim.State.CarriedItem);

        sim.Step(new PlayerCommand(0, -1, false)); // moving closes the prompt
        Assert.False(sim.State.RecyclePromptOpen);
        Assert.NotNull(sim.State.CarriedItem);

        sim.Use("recycle");
        var events = sim.Use("recycle");
        Assert.Null(sim.State.CarriedItem);
        Assert.Contains(events, e => e.Type == SimEventType.Recycled);
        Assert.Equal(0, sim.State.Score);
    }

    [Fact]
    public void Recycle_prompt_can_be_cancelled_without_moving()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        sim.Use("recycle");

        sim.Step(new PlayerCommand(0, 0, false, Cancel: true));

        Assert.False(sim.State.RecyclePromptOpen);
        Assert.NotNull(sim.State.CarriedItem);
    }
}
