using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests;

/// <summary>
/// GDD-default content built in code, so rule tests don't change when someone tunes the JSON files.
/// The dialogue book is empty on purpose: messages then come back as their keys ("hands_full"), which tests can check.
/// </summary>
public static class TestContent
{
    /// <param name="supply">Adds the supply sled, which makes kit bins limited (stocked once per shift from the order plan).</param>
    /// <param name="clock">Adds the punch clock by the door, where the elf can end the shift early.</param>
    public static GameContent Create(List<ChildLetter>? letters = null, Tuning? tuning = null, bool supply = false, bool clock = false) =>
        ContentLoader.Build(
            tuning ?? new Tuning(),
            [
                new Toy { Id = "train", Name = "Train", Parts = Parts("wheels", "boiler", "cab") },
                new Toy { Id = "bear", Name = "Bear", Parts = Parts("body", "arms", "head") },
                new Toy { Id = "robot", Name = "Robot", Parts = Parts("legs", "torso", "head") },
            ],
            [
                Station("kits_train", StationType.Dispenser, 180, 145, 90, 40, 180, 180, "train"),
                Station("kits_bear", StationType.Dispenser, 420, 145, 90, 40, 420, 180, "bear"),
                Station("kits_robot", StationType.Dispenser, 660, 145, 90, 40, 660, 180, "robot"),
                Station("bench", StationType.Bench, 300, 255, 120, 40, 300, 285),
                Station("ribbon", StationType.Ribbon, 610, 255, 100, 40, 610, 285),
                Station("recycle", StationType.Recycle, 120, 420, 50, 50, 165, 420),
                Station("shipping", StationType.Shipping, 830, 420, 70, 60, 780, 420),
                Station("coal", StationType.Coal, 66, 300, 24, 24, 104, 300),
                .. (supply ? [Station("supply", StationType.Supply, 904, 280, 48, 80, 858, 280)] : Array.Empty<Station>()),
                .. (clock ? [Station("clock", StationType.Clock, 29, 164, 18, 40, 62, 164)] : Array.Empty<Station>()),
            ],
            [
                new SantaEvent { Id = "presentation", Type = SantaEventType.RibbonWindow, WarnAt = 55, StartAt = 60, EndAt = 90 },
                new SantaEvent { Id = "executive", Type = SantaEventType.Rush, WarnAt = 115, StartAt = 120 },
                new SantaEvent { Id = "compliance", Type = SantaEventType.RibbonWindow, WarnAt = 175, StartAt = 180, EndAt = 210 },
                new SantaEvent { Id = "last_minute", Type = SantaEventType.Rush, WarnAt = 235, StartAt = 240 },
            ],
            new DialogueBook(),
            letters ?? []);

    public static List<ToyPart> Parts(params string[] ids) => ids.Select(id => new ToyPart { Id = id, Name = id }).ToList();

    private static Station Station(string id, StationType type, double x, double y, double w, double h, double ix, double iy, string? toy = null) =>
        new() { Id = id, Type = type, ToyId = toy, Label = id, Body = new Box2(x, y, w, h), Interact = new Point2(ix, iy) };
}

/// <summary>Small helpers that make rule tests read like play steps.</summary>
public static class SimExtensions
{
    public static Station StationById(this Simulation sim, string id) => sim.Content.Stations.First(s => s.Id == id);

    /// <summary>Walks (teleports) to a station and presses interact once. Returns the events from that step.</summary>
    public static IReadOnlyList<SimEvent> Use(this Simulation sim, string stationId)
    {
        sim.PlacePlayer(sim.StationById(stationId).Interact);
        sim.DrainEvents();
        sim.Step(new PlayerCommand(0, 0, Interact: true));
        return sim.DrainEvents();
    }

    /// <summary>Steps with no input.</summary>
    public static void Wait(this Simulation sim, long ticks)
    {
        for (long i = 0; i < ticks; i++) sim.Step(PlayerCommand.None);
    }

    public static void WaitSeconds(this Simulation sim, double seconds) => sim.Wait(sim.Tuning.Ticks(seconds));

    /// <summary>Finishes whatever the elf is doing: presses the right keys in the bench minigame, waits out a ribbon.</summary>
    public static void FinishCrafting(this Simulation sim)
    {
        while (sim.State.Assembly is { } game) sim.PressPart(game.NextKey);
        if (sim.State.RibbonPick != null) sim.PressPart(1); // the first spool
        while (sim.State.CraftingAction != null) sim.Step(PlayerCommand.None);
    }

    /// <summary>Presses a number key in the bench minigame. Returns the events from that step.</summary>
    public static IReadOnlyList<SimEvent> PressPart(this Simulation sim, int key)
    {
        sim.DrainEvents();
        sim.Step(new PlayerCommand(0, 0, false, PartKey: key));
        return sim.DrainEvents();
    }

    /// <summary>Collects a kit and builds it at the bench (pressing the right parts in order).</summary>
    public static void MakeToy(this Simulation sim, string toyId, bool ribbon = false)
    {
        sim.Use("kits_" + toyId);
        sim.Use("bench");
        sim.FinishCrafting();
        if (!ribbon) return;
        sim.Use("ribbon");
        sim.FinishCrafting();
    }
}
