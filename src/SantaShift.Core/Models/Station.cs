namespace SantaShift.Core.Models;

public enum StationType { Dispenser, Bench, Ribbon, Shipping, Recycle }

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
}
