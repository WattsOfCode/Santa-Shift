namespace SantaShift.Core.Models;

public enum SantaEventType { RibbonWindow, Rush }

/// <summary>One of Santa's scheduled policy changes. Loaded from events.json (times in seconds).</summary>
public sealed class SantaEvent
{
    public string Id { get; init; } = "";
    public SantaEventType Type { get; init; }
    public double WarnAt { get; init; }
    public double StartAt { get; init; }

    /// <summary>
    /// The window includes StartAt and excludes EndAt. Ribbon windows need it; a rush event with it makes every order
    /// created in the window RUSH, and without it only the next order.
    /// </summary>
    public double? EndAt { get; init; }

    public string? WarningCue { get; init; }
    public string? StartCue { get; init; }
}
