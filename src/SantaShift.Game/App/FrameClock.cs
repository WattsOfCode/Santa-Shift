namespace SantaShift.Game.App;

/// <summary>
/// Where the drawn frame falls between simulation steps. The rules step 60 times a second, but the game draws up to
/// 120 (or more) frames a second: views draw moving things part of the way from their last step to their current one
/// (<see cref="StepFraction"/> = 0..1), so motion is smooth at any frame rate instead of moving every other frame.
/// </summary>
public static class FrameClock
{
    /// <summary>How far the drawn frame is past the last simulation step, as a fraction of a step (0..1).</summary>
    public static float StepFraction { get; set; }
}
