namespace SantaShift.Core.Models;

public enum OrderStatus { Live, Delivered, Missed, Unfinished }

/// <summary>A toy request with a deadline. Requirements are copied in at creation and never change.</summary>
public sealed class Order
{
    public int Id { get; init; }
    public required string ToyId { get; init; }
    public bool RequiresRibbon { get; init; }
    public bool IsRush { get; init; }
    public long CreatedTick { get; init; }
    public long DeadlineTick { get; init; }
    public long AllowedDurationTicks { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Live;

    /// <summary>The letter shown on the order card, if one was available.</summary>
    public ChildLetter? Letter { get; init; }

    public long RemainingTicks(long tick) => Math.Max(0, DeadlineTick - tick);
}
