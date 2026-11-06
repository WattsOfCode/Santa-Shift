namespace SantaShift.Core.Models;

/// <summary>A toy the workshop can build. Loaded from toys.json.</summary>
public sealed class Toy
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double AssembleSeconds { get; init; } = 2.2;

    /// <summary>Model file for the finished toy, e.g. "toy_train.glb". A missing file falls back to grey-box shapes.</summary>
    public string? Model { get; init; }

    /// <summary>Hex colour used by grey-box shapes and UI icons, e.g. "#B94146".</summary>
    public string Color { get; init; } = "#8C6043";
}
