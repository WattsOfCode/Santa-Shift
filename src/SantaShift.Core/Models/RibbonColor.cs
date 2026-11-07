namespace SantaShift.Core.Models;

/// <summary>A ribbon colour on offer at the ribbon station (tuning.json > ribbonColors). Spool n is on part key n.</summary>
public sealed class RibbonColor
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>"#RRGGBB", for the spool, the bow and the order card.</summary>
    public string Color { get; init; } = "#E8BB57";
}

/// <summary>The elf is at the ribbon station choosing a colour (the camera is zoomed in on the spools).</summary>
public sealed class RibbonPickState
{
    public required string StationId { get; init; }
    public int ItemRevision { get; init; }
    public long StartTick { get; init; }
}
