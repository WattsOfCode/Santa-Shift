namespace SantaShift.Core.Models;

/// <summary>Timed actions (tying a ribbon, shovelling coal). Assembly is a minigame instead: see <see cref="AssemblyState"/>.</summary>
public enum CraftingType { Ribbon, Shovel }

/// <summary>A timed action in progress at the ribbon station or the coal scuttle.</summary>
public sealed class CraftingAction
{
    public required string StationId { get; init; }
    public CraftingType Type { get; init; }
    public int ItemRevision { get; init; }
    public long StartTick { get; init; }
    public long CompletionTick { get; init; }

    /// <summary>Ribbons: the colour being tied on.</summary>
    public string? RibbonColor { get; init; }

    public double Progress(long tick)
    {
        long total = CompletionTick - StartTick;
        return total <= 0 ? 1 : Math.Clamp((double)(tick - StartTick) / total, 0, 1);
    }
}
