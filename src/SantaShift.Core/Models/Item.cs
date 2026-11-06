namespace SantaShift.Core.Models;

public enum ItemStage { Kit, Assembled }

/// <summary>The one item the elf can carry.</summary>
public sealed class Item
{
    public required string ToyId { get; init; }
    public ItemStage Stage { get; set; } = ItemStage.Kit;
    public bool HasRibbon { get; set; }

    /// <summary>Changes every time the item changes, so a stale crafting action can never modify it.</summary>
    public int Revision { get; set; }
}
