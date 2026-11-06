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
    public void Assembly_takes_the_toys_assembly_time_and_transforms_the_same_item()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        var item = sim.State.CarriedItem;
        sim.Use("bench");

        sim.Wait(sim.Tuning.Ticks(2.2) - 1);
        Assert.Equal(ItemStage.Kit, item!.Stage);

        sim.Wait(1);
        Assert.Equal(ItemStage.Assembled, item.Stage);
        Assert.Same(item, sim.State.CarriedItem);
        Assert.Null(sim.State.CraftingAction);
    }

    [Fact]
    public void Moving_cancels_assembly_and_keeps_the_kit()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        sim.Use("bench");
        sim.Wait(60);

        sim.Step(new PlayerCommand(1, 0, false));
        var events = sim.DrainEvents();

        Assert.Null(sim.State.CraftingAction);
        Assert.Equal(ItemStage.Kit, sim.State.CarriedItem!.Stage);
        Assert.Contains(events, e => e.Type == SimEventType.ActionCancelled && e.Text == "assembly_cancelled");
    }

    [Fact]
    public void Pausing_cancels_assembly_silently_and_keeps_the_kit()
    {
        var sim = NewSim();
        sim.Use("kits_train");
        sim.Use("bench");
        sim.Wait(60);

        sim.Pause();

        Assert.Null(sim.State.CraftingAction);
        Assert.Equal(ItemStage.Kit, sim.State.CarriedItem!.Stage);
        Assert.DoesNotContain(sim.DrainEvents(), e => e.Type == SimEventType.ActionCancelled);
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
        var action = sim.State.CraftingAction;

        var events = sim.Use("bench");

        Assert.Same(action, sim.State.CraftingAction);
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
