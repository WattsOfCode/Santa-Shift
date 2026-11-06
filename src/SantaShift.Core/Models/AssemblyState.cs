namespace SantaShift.Core.Models;

/// <summary>
/// The bench minigame in progress: hammer the toy's parts on in the order shown, before time runs out.
/// Each part sits on a number key (Keys[0] is key 1). Pressing a part out of order, or running out of time,
/// botches the toy.
/// </summary>
public sealed class AssemblyState
{
    public required string StationId { get; init; }
    public required string ToyId { get; init; }
    public int ItemRevision { get; set; }

    /// <summary>Part ids by key: Keys[0] is on key 1, Keys[1] on key 2, and so on.</summary>
    public required IReadOnlyList<string> Keys { get; init; }

    /// <summary>The part ids in the order they must be added.</summary>
    public required IReadOnlyList<string> Order { get; init; }

    /// <summary>Parts added so far.</summary>
    public int Placed { get; set; }

    public long StartTick { get; init; }
    public long DeadlineTick { get; init; }

    public string? NextPart => Placed < Order.Count ? Order[Placed] : null;

    /// <summary>The key (1-based) that adds the next part, or 0 when done.</summary>
    public int NextKey => NextPart is { } id ? Keys.ToList().IndexOf(id) + 1 : 0;

    /// <summary>1 at the start, 0 when time is up.</summary>
    public double TimeLeft(long tick)
    {
        long total = DeadlineTick - StartTick;
        return total <= 0 ? 0 : Math.Clamp((double)(DeadlineTick - tick) / total, 0, 1);
    }
}
