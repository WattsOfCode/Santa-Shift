namespace SantaShift.Core.Models;

public enum RunPhase { Playing, Paused, Results }

/// <summary>Everything about one shift. Rules change it; views only read it.</summary>
public sealed class RunState
{
    public GameMode Mode { get; init; }

    /// <summary>1 for the first shift of a run; later rounds have a higher quota and shorter deadlines.</summary>
    public int Round { get; init; } = 1;

    public RunPhase Phase { get; set; } = RunPhase.Playing;

    /// <summary>Children on this run's naughty list. Their orders want coal, not toys.</summary>
    public IReadOnlySet<string> NaughtyChildren { get; init; } = new HashSet<string>();

    /// <summary>Simulation steps completed. 60 ticks = 1 second.</summary>
    public long Tick { get; set; }

    // Player
    public Point2 PlayerPosition { get; set; }
    public Item? CarriedItem { get; set; }
    public CraftingAction? CraftingAction { get; set; }

    /// <summary>The bench minigame, while the elf is building a toy.</summary>
    public AssemblyState? Assembly { get; set; }

    /// <summary>Choosing a ribbon colour at the ribbon station, before tying.</summary>
    public RibbonPickState? RibbonPick { get; set; }

    /// <summary>True while the elf is busy at a station (building, choosing or tying a ribbon, shovelling).</summary>
    public bool IsWorking => CraftingAction != null || Assembly != null || RibbonPick != null;

    /// <summary>The Christmas Spirit meter (0 to <see cref="SpiritTuning.Max"/>), carried over from earlier rounds.</summary>
    public double Spirit { get; set; } = 100;

    /// <summary>True when the shift ended early because the spirit ran out.</summary>
    public bool SpiritOut { get; set; }

    /// <summary>Lumps of coal delivered to naughty children this shift.</summary>
    public int CoalDelivered { get; set; }

    /// <summary>The guided first round: arrows lead the way and the first orders are arranged as a lesson.</summary>
    public bool Tutorial { get; init; }

    /// <summary>The guided orders so far (ids), up to <see cref="Content.TutorialTuning.Orders"/>.</summary>
    public HashSet<int> TutorialOrderIds { get; } = [];

    /// <summary>The perks picked so far this run, combined.</summary>
    public RunModifiers Modifiers { get; init; } = RunModifiers.None;
    public bool RecyclePromptOpen { get; set; }

    // Kit bins
    /// <summary>True when kit bins hold limited stock (the room has a supply sled). Otherwise bins never run out.</summary>
    public bool LimitedKits { get; init; }

    /// <summary>Kits left in each bin, by toy id (only used when <see cref="LimitedKits"/>).</summary>
    public Dictionary<string, int> KitStock { get; } = new();

    /// <summary>Kits left in the bin for a toy, or null if bins are unlimited.</summary>
    public int? KitsLeft(string toyId) => LimitedKits ? KitStock.GetValueOrDefault(toyId) : null;

    // Orders
    public List<Order> LiveOrders { get; } = new();
    public List<Order> ClosedOrders { get; } = new();
    public int NextSpawnIndex { get; set; }
    public int OrdersCreated { get; set; }
    public int SkippedSpawns { get; set; }
    public Dictionary<string, int> LetterCursor { get; } = new();

    // Results
    public int Delivered { get; set; }
    public int Missed { get; set; }
    public int Unfinished { get; set; }
    public int Score { get; set; }
    public int Streak { get; set; }
    public int BestStreak { get; set; }
    public bool QuotaMetAnnounced { get; set; }

    public EventState Events { get; } = new();

    private int _nextId = 1;

    /// <summary>IDs increase within a run and restart with each new RunState.</summary>
    public int NextId() => _nextId++;
}
