namespace SantaShift.Core.Models;

/// <summary>Santa's current policies during a run.</summary>
public sealed class EventState
{
    /// <summary>New orders need ribbons while the tick is below this value.</summary>
    public long RibbonWindowEndTick { get; set; } = -1;

    /// <summary>The next order actually created will be RUSH.</summary>
    public bool PendingRush { get; set; }

    public HashSet<string> FiredEventIds { get; } = new();

    public bool RibbonActive(long tick) => tick < RibbonWindowEndTick;
}
