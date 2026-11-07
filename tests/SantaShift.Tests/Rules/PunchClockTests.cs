using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>
/// The punch clock by the door: pressing use there asks first, and pressing again ends the shift early, scored as if
/// time had run out.
/// </summary>
public class PunchClockTests
{
    private static readonly List<ChildLetter> Letters =
    [
        new() { Id = "ann", Child = "Ann", Age = 6, ToyId = "train", Line = "Choo" },
    ];

    private static Simulation NewSim(int quota = 9) =>
        new(TestContent.Create(Letters, new Tuning { Quota = quota }, clock: true), GameMode.Standard);

    [Fact]
    public void The_first_press_asks_and_the_second_punches_out()
    {
        var sim = NewSim();
        sim.WaitSeconds(10);
        var first = sim.Use("clock");
        Assert.Contains(first, e => e.Type == SimEventType.PunchPrompt);
        Assert.True(sim.State.PunchPromptOpen);
        Assert.Equal(RunPhase.Playing, sim.State.Phase);
        Assert.Equal("prompt_punch_confirm_short", sim.Prompt());

        var second = sim.Use("clock");
        Assert.Contains(second, e => e.Type == SimEventType.ShiftEnded);
        Assert.Equal(RunPhase.Results, sim.State.Phase);
        Assert.True(sim.State.PunchedOut);
        Assert.Equal(sim.Tuning.ShiftTicks - sim.State.Tick, sim.State.TicksLeftAtPunchOut);
        Assert.True(sim.State.TicksLeftAtPunchOut > sim.Tuning.Ticks(280));
    }

    [Fact]
    public void Moving_or_cancelling_closes_the_question()
    {
        var sim = NewSim();
        sim.Use("clock");
        sim.Step(new PlayerCommand(1, 0, false));
        Assert.False(sim.State.PunchPromptOpen);

        sim.Use("clock");
        sim.Step(new PlayerCommand(0, 0, false, Cancel: true));
        Assert.False(sim.State.PunchPromptOpen);
        Assert.Equal(RunPhase.Playing, sim.State.Phase);
    }

    [Fact]
    public void Pausing_closes_the_question()
    {
        var sim = NewSim();
        sim.Use("clock");
        sim.Pause();
        sim.Resume();
        Assert.False(sim.State.PunchPromptOpen);
        sim.Use("clock"); // asks again rather than punching out
        Assert.Equal(RunPhase.Playing, sim.State.Phase);
    }

    [Fact]
    public void Open_orders_count_as_unfinished_and_no_more_letters_arrive()
    {
        var sim = NewSim();
        sim.WaitSeconds(20); // orders at 0 and 18 s
        int open = sim.State.LiveOrders.Count;
        Assert.True(open > 0);
        sim.Use("clock");
        sim.Use("clock");
        Assert.Equal(open, sim.State.Unfinished);
        Assert.Equal(0, sim.State.Missed);
        Assert.Empty(sim.State.LiveOrders);

        long tick = sim.State.Tick;
        sim.WaitSeconds(60);
        Assert.Equal(tick, sim.State.Tick); // the shift is over: nothing moves on
    }

    [Fact]
    public void With_the_quota_met_Santa_signs_off_on_the_early_finish()
    {
        var sim = NewSim(quota: 1);
        sim.Wait(1);
        sim.MakeToy("train");
        sim.Use("shipping");
        Assert.Equal(1, sim.State.Delivered);
        Assert.Equal("prompt_punch_out", PromptAt(sim, "clock"));

        sim.Use("clock");
        Assert.Equal("prompt_punch_confirm", sim.Prompt());
        sim.Use("clock");
        Assert.Equal("punched_out", sim.ReviewCueId);
    }

    [Fact]
    public void Short_of_quota_punching_out_is_a_missed_shift()
    {
        var sim = NewSim();
        Assert.Equal("prompt_punch_out_short", PromptAt(sim, "clock"));
        sim.Use("clock");
        sim.Use("clock");
        Assert.Equal("review_miss", sim.ReviewCueId);
    }

    [Fact]
    public void A_shift_that_runs_its_course_is_not_punched_out()
    {
        var sim = NewSim(quota: 0);
        sim.Wait(sim.Tuning.ShiftTicks + 1);
        Assert.Equal(RunPhase.Results, sim.State.Phase);
        Assert.False(sim.State.PunchedOut);
        Assert.Equal(0, sim.State.TicksLeftAtPunchOut);
        Assert.Equal("review_pass", sim.ReviewCueId);
    }

    [Fact]
    public void The_shipped_room_has_a_punch_clock_and_a_solid_door()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        var clock = Assert.Single(content.Stations, s => s.Type == StationType.Clock);
        Assert.Contains(content.Props, p => p.Model == "prop_door");
        // The door blocks the elf: there is an obstacle where it stands on the back wall.
        var door = content.Props.First(p => p.Model == "prop_door").Position;
        Assert.Contains(content.Colliders, c => Math.Abs(c.X - door.X) < 1 && c.Bottom >= content.Floor.Top);
        Assert.DoesNotContain(content.Warnings, w => w.Contains(clock.Id));
    }

    private static string? PromptAt(Simulation sim, string stationId)
    {
        sim.PlacePlayer(sim.StationById(stationId).Interact);
        return sim.Prompt();
    }
}
