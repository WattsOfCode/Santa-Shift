namespace SantaShift.Core.Models;

public enum OrderStatus { Live, Delivered, Missed, Unfinished }

/// <summary>A toy request with a deadline. Requirements are copied in at creation and never change.</summary>
public sealed class Order
{
    public int Id { get; init; }
    public required string ToyId { get; init; }
    public bool RequiresRibbon { get; init; }

    /// <summary>The ribbon colour asked for (a <see cref="RibbonColor"/> id), or null if any ribbon will do.</summary>
    public string? RibbonColor { get; init; }
    public bool IsRush { get; init; }

    /// <summary>The child is on the naughty list: they get a lump of coal instead of the toy they asked for.</summary>
    public bool Naughty { get; init; }
    public long CreatedTick { get; init; }
    public long DeadlineTick { get; init; }
    public long AllowedDurationTicks { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Live;

    /// <summary>The letter shown on the order card, if one was available.</summary>
    public ChildLetter? Letter { get; init; }

    public long RemainingTicks(long tick) => Math.Max(0, DeadlineTick - tick);
}
