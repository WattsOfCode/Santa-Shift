using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Controllers;

/// <summary>Turns movement keys into a movement command, and tracks facing and bob for the elf's view.</summary>
public sealed class PlayerController(IInputService input, Settings settings)
{
    private float _phase;

    public float FacingDegrees { get; private set; }
    public float Bob { get; private set; }

    /// <summary>Reads movement for this step and updates the elf's facing and bob animation.</summary>
    public (double X, double Y) ReadMove(bool working)
    {
        var move = input.Move();
        bool walking = move.LengthSquared() > 0;

        if (walking) FacingDegrees = MathF.Atan2(move.X, move.Y) * 180f / MathF.PI; // 0 = towards camera

        if (settings.ReducedMotion || (!walking && !working)) { _phase = 0; Bob = 0; }
        else
        {
            _phase += working ? 0.5f : 0.3f;
            Bob = MathF.Abs(MathF.Sin(_phase)) * (working ? 0.04f : 0.06f);
        }
        return (move.X, move.Y);
    }
}
