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

    /// <summary>Furniture and decorations, each drawn from its own model file.</summary>
    public List<PropPlacement> Props { get; init; } = [];

    /// <summary>Point lights: placed in room.blend or carried by props (fireplace, lanterns, windows).</summary>
    public List<RoomLight> Lights { get; init; } = [];

    /// <summary>Soft halos around light sources (tree bulbs, the star), drawn by the game; they light nothing.</summary>
    public List<RoomGlow> Glows { get; init; } = [];

    /// <summary>Workshop elves at their own benches (scenery).</summary>
    public List<NpcPlacement> Npcs { get; init; } = [];
}

/// <summary>
/// A point light in game-world metres (Y is height). Drawing only: it never affects the rules.
/// Intensity ~1 is a lantern, ~3 a fireplace; Radius is how far it reaches; Flicker 0..1 makes it waver like fire.
/// </summary>
/// <summary>A halo at a light source: position in metres (Y = height), colour 0..1, width in metres, twinkle 0..1.</summary>
public sealed class RoomGlow
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public double[] Color { get; init; } = [1, 1, 1];
    public double Size { get; init; } = 0.2;
    public double Twinkle { get; init; }
}

public sealed class RoomLight
{
    public string Name { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public double[] Color { get; init; } = [1, 1, 1];
    public double Intensity { get; init; } = 1;
    public double Radius { get; init; } = 5;
    public double Flicker { get; init; }
}

/// <summary>A workshop elf in game-world metres, facing Yaw degrees (0 = +Z).</summary>
public sealed class NpcPlacement
{
    public double X { get; init; }
    public double Z { get; init; }
    public double Yaw { get; init; }
}

public sealed class PropPlacement
{
    /// <summary>Model name without extension, e.g. "prop_tree" (drawn from prop_tree.glb).</summary>
    public string Model { get; init; } = "";
    public double X { get; init; }
    public double Z { get; init; }
    public double Yaw { get; init; }
    public double Scale { get; init; } = 1;
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
