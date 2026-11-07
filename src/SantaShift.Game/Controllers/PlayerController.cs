using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;

namespace SantaShift.Game.Controllers;

/// <summary>
/// Turns the pressed direction into smooth movement for the rules, and tracks facing and bob for the elf's view.
/// The elf's heading turns towards the pressed direction at a limited rate and it speeds up over a moment, so it makes
/// natural turns instead of snapping; on a sharp turn it slows down while it comes round (a small U-turn, not a slide).
/// Letting go stops it at once, so it never drifts away from a station.
/// </summary>
public sealed class PlayerController(IInputService input, Settings settings, MovementSettings movement)
{
    private float _phase;
    private float _heading = MathF.PI; // radians; 0 = facing +Z. Starts facing the back wall.
    private float _speed;              // 0..1 of full speed

    /// <summary>0 degrees = facing +Z.</summary>
    public float FacingDegrees => _heading * 180f / MathF.PI;
    public float Bob { get; private set; }

    /// <summary>True while the elf is walking (for the walk animation).</summary>
    public bool Walking { get; private set; }

    public IInputService Input => input;

    /// <param name="floorMove">The pressed direction, already turned into a floor direction (see CameraController).</param>
    /// <param name="faceWork">While building, the floor direction towards the work, so the elf turns to face it.</param>
    /// <param name="dt">Seconds per simulation step.</param>
    /// <returns>The movement to send to the rules this step (length 0..1).</returns>
    public (double X, double Y) ReadMove((double X, double Y) floorMove, bool working, (double X, double Y)? faceWork = null,
        float dt = 1f / 60)
    {
        float length = MathF.Sqrt((float)(floorMove.X * floorMove.X + floorMove.Y * floorMove.Y));
        float maxTurn = movement.TurnDegreesPerSecond * MathF.PI / 180f * dt;
        (double X, double Y) result = (0, 0);

        if (length > 0.001f)
        {
            float wanted = MathF.Atan2((float)floorMove.X, (float)floorMove.Y);
            float diff = AngleDiff(wanted, _heading);
            _heading = Wrap(_heading + Math.Clamp(diff, -maxTurn, maxTurn));

            // Ease off while the heading is still far from where the player is pushing (cos 0 = 1, cos 90+ = slow).
            float targetSpeed = length * Math.Max(movement.TurningSpeedFloor, MathF.Cos(Math.Min(MathF.Abs(diff), MathF.PI / 2)));
            float accel = movement.AccelerationSeconds > 0 ? dt / movement.AccelerationSeconds : 1;
            _speed = _speed < targetSpeed ? Math.Min(targetSpeed, _speed + accel) : Math.Max(targetSpeed, _speed - accel * 2);
            result = (MathF.Sin(_heading) * _speed, MathF.Cos(_heading) * _speed);
        }
        else
        {
            _speed = 0;
            if (faceWork is { } f && (f.X != 0 || f.Y != 0))
            {
                float diff = AngleDiff(MathF.Atan2((float)f.X, (float)f.Y), _heading);
                _heading = Wrap(_heading + Math.Clamp(diff, -maxTurn, maxTurn));
            }
        }

        Walking = _speed > 0.01f;
        if (settings.ReducedMotion || (!Walking && !working)) { _phase = 0; Bob = 0; }
        else
        {
            _phase += working ? 0.5f : 0.3f;
            Bob = MathF.Abs(MathF.Sin(_phase)) * (working ? 0.04f : 0.06f);
        }
        return result;
    }

    /// <summary>Points the elf at once (debug screenshots, round starts).</summary>
    public void FaceInstantly(float degrees) => _heading = Wrap(degrees * MathF.PI / 180f);

    /// <summary>Shortest signed angle from <paramref name="from"/> to <paramref name="to"/>, in -pi..pi.</summary>
    public static float AngleDiff(float to, float from) => Wrap(to - from);

    private static float Wrap(float a)
    {
        while (a > MathF.PI) a -= 2 * MathF.PI;
        while (a < -MathF.PI) a += 2 * MathF.PI;
        return a;
    }
}
