namespace SantaShift.Core.Models;

/// <summary>
/// The room as designed in Blender, written by tools/blender/export.ps1 to Content/data/room_layout.json.
/// Everything is in game-world metres: x to the right, z towards the player's starting side.
/// When the file exists it overrides the floor size and station positions in tuning.json / stations.json,
/// and adds extra obstacles.
/// </summary>
public sealed class RoomLayout
{
    public int SchemaVersion { get; init; } = 1;
    public string? Source { get; init; }
    public MetreRect? Floor { get; init; }

    /// <summary>Where the elf starts each round (optional).</summary>
    public MetrePoint? PlayerStart { get; init; }
    public List<MetreBox> Colliders { get; init; } = [];
    public Dictionary<string, StationPlacement> Stations { get; init; } = new();
}

public sealed class MetrePoint
{
    public double X { get; init; }
    public double Z { get; init; }
}

public sealed class MetreRect
{
    public double MinX { get; init; }
    public double MinZ { get; init; }
    public double MaxX { get; init; }
    public double MaxZ { get; init; }
}

/// <summary>A box on the floor: centre x/z and size w (along x) / d (along z), in metres.</summary>
public sealed class MetreBox
{
    public string Name { get; init; } = "";
    public double X { get; init; }
    public double Z { get; init; }
    public double W { get; init; }
    public double D { get; init; }
}

public sealed class StationPlacement
{
    public double X { get; init; }
    public double Z { get; init; }
    public double W { get; init; }
    public double D { get; init; }
    public double InteractX { get; init; }
    public double InteractZ { get; init; }

    /// <summary>Turn of the station model around the vertical axis, in degrees (visual only).</summary>
    public double Yaw { get; init; }
}
