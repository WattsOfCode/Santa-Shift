namespace SantaShift.Core.Models;

/// <summary>Timed actions (the ribbon). Assembly is a minigame instead: see <see cref="AssemblyState"/>.</summary>
public enum CraftingType { Ribbon }

/// <summary>A timed action in progress at the ribbon station.</summary>
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
