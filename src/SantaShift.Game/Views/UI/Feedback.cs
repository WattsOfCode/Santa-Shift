namespace SantaShift.Game.Views.UI;

public enum ToastKind { Info, Success, Rejected }

/// <summary>
/// What the HUD should currently show besides the game state: the latest functional message
/// and Santa's latest announcement. Filled by the InteractionController from simulation events.
/// </summary>
public sealed class Feedback
{
    public string? Toast { get; private set; }
    public ToastKind ToastKind { get; private set; }
    public int ToastTicks { get; private set; }

    public string? SantaLine { get; private set; }
    public string? SantaText { get; private set; }
    public int SantaTicks { get; private set; }

    public string? LetterText { get; private set; }

    public void ShowToast(string text, ToastKind kind, int ticks = 120)
    {
        Toast = text; ToastKind = kind; ToastTicks = ticks;
    }

    /// <summary>Santa's flavour line fades after 5 seconds; the rule itself stays on the order cards (GDD section 10).</summary>
    public void ShowSanta(string? line, string? text, int ticks = 300)
    {
        SantaLine = line; SantaText = text; SantaTicks = ticks;
    }

    public void ShowLetter(string text) => LetterText = text;

    public void Tick()
    {
        if (ToastTicks > 0 && --ToastTicks == 0) Toast = null;
        if (SantaTicks > 0 && --SantaTicks == 0) { SantaLine = null; SantaText = null; }
    }
}
