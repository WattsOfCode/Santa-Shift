namespace SantaShift.Core.Models;

public enum CraftingType { Assemble, Ribbon }

/// <summary>A timed action in progress at a bench or ribbon station.</summary>
public sealed class CraftingAction
{
    public required string StationId { get; init; }
    public CraftingType Type { get; init; }
    public int ItemRevision { get; init; }
    public long StartTick { get; init; }
    public long CompletionTick { get; init; }

    public double Progress(long tick)
    {
        long total = CompletionTick - StartTick;
        return total <= 0 ? 1 : Math.Clamp((double)(tick - StartTick) / total, 0, 1);
    }
}
