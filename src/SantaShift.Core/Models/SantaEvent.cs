namespace SantaShift.Core.Models;

public enum SantaEventType { RibbonWindow, Rush }

/// <summary>One of Santa's scheduled policy changes. Loaded from events.json (times in seconds).</summary>
public sealed class SantaEvent
{
    public string Id { get; init; } = "";
    public SantaEventType Type { get; init; }
    public double WarnAt { get; init; }
    public double StartAt { get; init; }

    /// <summary>Ribbon windows only: the window includes StartAt and excludes EndAt.</summary>
    public double? EndAt { get; init; }

    public string? WarningCue { get; init; }
    public string? StartCue { get; init; }
}
