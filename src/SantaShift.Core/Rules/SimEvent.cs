namespace SantaShift.Core.Rules;

public enum SimEventType
{
    Collected, ShovelStarted, CoalCollected, SupplyTaken, Restocked, AssemblyStarted, PartAdded, AssemblyComplete, AssemblyBotched,
    RibbonPickStarted, RibbonStarted, RibbonComplete, ActionCancelled,
    Delivered, Rejected, RecyclePrompt, Recycled, PunchPrompt,
    OrderCreated, OrderExpired, PolicyWarning, PolicyStarted, QuotaMet, ShiftEnded,
}

/// <summary>
/// Something that just happened in the simulation. Views, sound and particles react to these;
/// they never change score or state themselves (GDD section 18).
/// </summary>
/// <param name="Text">Plain mechanical message for the player, if any.</param>
/// <param name="SantaLine">Optional flavour line from Santa.</param>
public sealed record SimEvent(
    SimEventType Type,
    string? Text = null,
    string? SantaLine = null,
    string? ToyId = null,
    int OrderId = 0,
    int Points = 0);

/// <summary>The player's input for one simulation step.</summary>
/// <param name="MoveX">-1..1, already normalized with MoveY.</param>
/// <param name="Interact">True only on the step the key was pressed (never while held).</param>
/// <param name="Cancel">Closes an open recycle or punch-out prompt.</param>
/// <param name="PartKey">A number key pressed this step for the bench minigame (1-based), or 0.</param>
public readonly record struct PlayerCommand(double MoveX, double MoveY, bool Interact, bool Cancel = false, int PartKey = 0)
{
    public bool IsMoving => MoveX != 0 || MoveY != 0;
    public static readonly PlayerCommand None = new(0, 0, false);
}
