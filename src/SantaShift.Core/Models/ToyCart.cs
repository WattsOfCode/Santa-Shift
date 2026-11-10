namespace SantaShift.Core.Models;

/// <summary>
/// The toy cart during a shift. It is built over the run (one part per passed round, see <see cref="RunProgress.CartParts"/>)
/// and works once every part is on: finished toys can be loaded into it (up to its capacity) and taken out again one
/// at a time, and the elf can push it around the room, slower than walking, with empty hands.
/// </summary>
public sealed class ToyCart
{
    /// <summary>Parts built so far this run (0 = none). The cart works when all are on.</summary>
    public int Parts { get; init; }

    /// <summary>True when every part is on.</summary>
    public bool Built { get; init; }

    /// <summary>Where the cart stands (logical pixels): its home at the start of the shift, then wherever it was left.</summary>
    public Point2 Position { get; set; }

    /// <summary>
    /// The direction the cart points (a unit floor vector): while pushed, it swings round towards where the elf walks,
    /// and the cart stays that far ahead of the elf.
    /// </summary>
    public (double X, double Y) Direction { get; set; } = (0, 1);

    /// <summary>The toys in the cart, in the order they were put in (slot 1 first).</summary>
    public List<Item> Items { get; } = [];

    /// <summary>True while the elf is pushing the cart (hands on the handle, so they carry nothing else).</summary>
    public bool Pushing { get; set; }

    /// <summary>True while the elf is choosing what to do at the cart: take toy n (part key n) or push it (use).</summary>
    public bool PickOpen { get; set; }
}
