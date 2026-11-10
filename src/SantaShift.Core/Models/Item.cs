namespace SantaShift.Core.Models;

/// <summary>
/// Kit (from a bin), Assembled (all parts in the right order), Botched (wrong order or too slow: scrap it),
/// Coal (a lump from the fireplace, for a child on the naughty list), or Supply (a crate of spare kits from the supply
/// sled, carried to a kit bin to restock it).
/// </summary>
public enum ItemStage { Kit, Assembled, Botched, Coal, Supply }

/// <summary>The one item the elf can carry.</summary>
public sealed class Item
{
    /// <summary>The <see cref="ToyId"/> of a lump of coal (it isn't a toy).</summary>
    public const string CoalId = "coal";

    /// <summary>The <see cref="ToyId"/> of a supply crate (it isn't a toy either).</summary>
    public const string SupplyId = "supply";

    public required string ToyId { get; init; }
    public ItemStage Stage { get; set; } = ItemStage.Kit;
    public bool HasRibbon { get; set; }

    /// <summary>The colour of its ribbon (a <see cref="RibbonColor"/> id), or null for a plain ribbon / none.</summary>
    public string? RibbonColor { get; set; }

    /// <summary>How many parts are on the toy so far while it is being built (0 for a fresh kit).</summary>
    public int PartsPlaced { get; set; }

    /// <summary>Which variant was built (see <see cref="Toy.Variants"/>): null for the plain toy, and for kits.</summary>
    public string? Variant { get; set; }

    /// <summary>Changes every time the item changes, so a stale crafting action can never modify it.</summary>
    public int Revision { get; set; }

    public bool IsCoal => Stage == ItemStage.Coal;

    public bool IsSupply => Stage == ItemStage.Supply;

    public static Item Coal(int revision) => new() { ToyId = CoalId, Stage = ItemStage.Coal, Revision = revision };

    public static Item Supply(int revision) => new() { ToyId = SupplyId, Stage = ItemStage.Supply, Revision = revision };
}
