namespace SantaShift.Core.Models;

/// <summary>Kit (from a bin), Assembled (all parts in the right order), or Botched (wrong order or too slow: scrap it).</summary>
public enum ItemStage { Kit, Assembled, Botched }

/// <summary>The one item the elf can carry.</summary>
public sealed class Item
{
    public required string ToyId { get; init; }
    public ItemStage Stage { get; set; } = ItemStage.Kit;
    public bool HasRibbon { get; set; }

    /// <summary>How many parts are on the toy so far while it is being built (0 for a fresh kit).</summary>
    public int PartsPlaced { get; set; }

    /// <summary>Changes every time the item changes, so a stale crafting action can never modify it.</summary>
    public int Revision { get; set; }
}
