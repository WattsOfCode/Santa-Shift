namespace SantaShift.Core.Models;

/// <summary>
/// Coal: the coal scuttle by the fireplace, where the elf grabs coal for children on the naughty list.
/// Supply: the supply sled, where the elf takes a crate of spare kits to restock an empty kit bin.
/// </summary>
public enum StationType { Dispenser, Bench, Ribbon, Shipping, Recycle, Coal, Supply }

/// <summary>A fixture in the workshop. The layout comes from stations.json.</summary>
public sealed class Station
{
    public string Id { get; init; } = "";
    public StationType Type { get; init; }

    /// <summary>Dispensers only: which toy kit this bin hands out.</summary>
    public string? ToyId { get; init; }

    public string Label { get; init; } = "";

    /// <summary>The solid body the player collides with.</summary>
    public Box2 Body { get; init; }

    /// <summary>Where the player stands to use it. Must sit outside the body.</summary>
    public Point2 Interact { get; init; }

    public string? Model { get; init; }

    /// <summary>Turn of the model around the vertical axis, in degrees. Visual only; the body stays axis-aligned.</summary>
    public double Yaw { get; init; }
}
