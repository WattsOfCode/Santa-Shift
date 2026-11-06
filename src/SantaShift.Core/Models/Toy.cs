namespace SantaShift.Core.Models;

/// <summary>A toy the workshop can build. Loaded from toys.json.</summary>
public sealed class Toy
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>
    /// The parts hammered together at the bench, in their natural build order (the order used in the easy rounds).
    /// Each part can have its own model, drawn on the bench as it is added.
    /// </summary>
    public List<ToyPart> Parts { get; init; } = [];

    /// <summary>Model file for the finished toy, e.g. "toy_train.glb". A missing file falls back to grey-box shapes.</summary>
    public string? Model { get; init; }

    /// <summary>Model for a toy built in the wrong order (or too slowly). It can only be scrapped.</summary>
    public string? BotchedModel { get; init; }

    /// <summary>Hex colour used by grey-box shapes and UI icons, e.g. "#B94146".</summary>
    public string Color { get; init; } = "#8C6043";
}

/// <summary>One piece of a toy, e.g. the train's wheels.</summary>
public sealed class ToyPart
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Model of this piece alone, placed where it sits on the finished toy (so pieces stack into the toy).</summary>
    public string? Model { get; init; }
}
