namespace SantaShift.Core.Models;

public enum RunPhase { Playing, Paused, Results }

/// <summary>Everything about one shift. Rules change it; views only read it.</summary>
public sealed class RunState
{
    public GameMode Mode { get; init; }

    /// <summary>1 for the first shift of a run; later rounds have a higher quota and shorter deadlines.</summary>
    public int Round { get; init; } = 1;

    public RunPhase Phase { get; set; } = RunPhase.Playing;

    /// <summary>Simulation steps completed. 60 ticks = 1 second.</summary>
    public long Tick { get; set; }

    // Player
    public Point2 PlayerPosition { get; set; }
    public Item? CarriedItem { get; set; }
    public CraftingAction? CraftingAction { get; set; }
    public bool RecyclePromptOpen { get; set; }

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
