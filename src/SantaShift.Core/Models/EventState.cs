namespace SantaShift.Core.Models;

/// <summary>Santa's current policies during a run.</summary>
public sealed class EventState
{
    /// <summary>New orders need ribbons while the tick is below this value.</summary>
    public long RibbonWindowEndTick { get; set; } = -1;

    /// <summary>New orders are RUSH while the tick is below this value.</summary>
    public long RushWindowEndTick { get; set; } = -1;

    public bool RushActive(long tick) => tick < RushWindowEndTick;

    /// <summary>The next order actually created will be RUSH (a rush event with no end time).</summary>
    public bool PendingRush { get; set; }

    public HashSet<string> FiredEventIds { get; } = new();

    public bool RibbonActive(long tick) => tick < RibbonWindowEndTick;
}
