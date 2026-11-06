using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Controllers;

/// <summary>Passes the movement direction to the rules, and tracks facing and bob for the elf's view.</summary>
public sealed class PlayerController(IInputService input, Settings settings)
{
    private float _phase;

    public float FacingDegrees { get; private set; } = 180; // start facing away from the camera, towards the back wall
    public float Bob { get; private set; }

    public IInputService Input => input;

    /// <param name="floorMove">Movement already turned into a floor direction (see CameraController).</param>
    public (double X, double Y) ReadMove((double X, double Y) floorMove, bool working)
    {
        bool walking = floorMove.X != 0 || floorMove.Y != 0;

        // Face the way we walk. 0 degrees = facing +Z; floor y matches world Z.
        if (walking) FacingDegrees = MathF.Atan2((float)floorMove.X, (float)floorMove.Y) * 180f / MathF.PI;

        if (settings.ReducedMotion || (!walking && !working)) { _phase = 0; Bob = 0; }
        else
        {
            _phase += working ? 0.5f : 0.3f;
            Bob = MathF.Abs(MathF.Sin(_phase)) * (working ? 0.04f : 0.06f);
        }
        return floorMove;
    }
}
