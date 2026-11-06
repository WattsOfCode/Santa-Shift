namespace SantaShift.Core.Models;

/// <summary>A child's letter. Each new order uses the next unused letter for its toy. Loaded from letters.json.</summary>
public sealed class ChildLetter
{
    public string Id { get; init; } = "";
    public string Child { get; init; } = "";
    public int Age { get; init; }
    public string ToyId { get; init; } = "";
    public string Line { get; init; } = "";
}
