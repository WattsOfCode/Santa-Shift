using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>Where the elf is working: the spot on the station top, and the floor direction from the elf towards it.</summary>
public readonly record struct WorkFocus(Vector3 Spot, Vector3 Direction);

/// <summary>
/// Third-person camera behind the elf. The mouse turns it (yaw) and tilts it (pitch);
/// movement keys are turned into floor directions relative to where it faces.
/// </summary>
public sealed class CameraController
{
    private readonly ChaseCameraSettings _cfg;
    private readonly WorldSpace _world;
    private readonly FloorBounds _floor;

    /// <summary>0 = looking towards -Z (the back wall, where the kit bins are).</summary>
    public float YawDegrees { get; private set; }
    public float PitchDegrees { get; private set; }

    /// <summary>Debug builds only (--yaw / --pitch): starting camera angle for automated screenshots.</summary>
    public static float? DebugStartYaw, DebugStartPitch;

    private readonly ShoulderCameraSettings _shoulder;

    public CameraController(ChaseCameraSettings cfg, ShoulderCameraSettings shoulder, WorldSpace world, GameContent content)
    {
        _cfg = cfg;
        _shoulder = shoulder;
        _world = world;
        _floor = content.Floor;
        PitchDegrees = DebugStartPitch ?? cfg.PitchDegrees;
        YawDegrees = DebugStartYaw ?? 0;
    }

    /// <summary>0 = normal chase view, 1 = fully over the elf's shoulder.</summary>
    public float ShoulderBlend { get; private set; }

    /// <summary>
    /// Applies mouse movement and repositions the camera behind the elf. Call once per frame.
    /// With <paramref name="work"/> set (building a toy), the camera swings in over the elf's right shoulder to watch
    /// the toy at <see cref="WorkFocus.Spot"/>; mouse look pauses, and afterwards the chase view carries on from behind the elf.
    /// </summary>
    /// <param name="facingDegrees">The elf's facing (0 = +Z), for the auto-follow: once it has faced one way for
    /// <see cref="ChaseCameraSettings.AutoFollowSeconds"/> with the mouse left alone, the camera eases round behind it.</param>
    public void Update(Vector2 mouseDelta, Point2 playerPosition, WorkFocus? work = null, float dt = 0, float? facingDegrees = null)
    {
        float step = _shoulder.BlendSeconds > 0 ? dt / _shoulder.BlendSeconds : 1;
        ShoulderBlend = Math.Clamp(ShoulderBlend + (work != null ? step : -step), 0, 1);
        bool mouseMoved = mouseDelta != Vector2.Zero;
        if (mouseMoved) _mouseMovedSinceInput = true;

        if (work is { } w)
        {
            // Turn the chase camera to look the way the elf is working, so leaving the close view is seamless.
            YawDegrees = MathF.Atan2(-w.Direction.X, -w.Direction.Z) * 180f / MathF.PI;
            _lastWork = w;
            _steadyTime = 0;
        }
        else
        {
            YawDegrees -= mouseDelta.X * _cfg.MouseSensitivity;
            float dy = mouseDelta.Y * _cfg.MouseSensitivity * (_cfg.InvertY ? -1 : 1);
            PitchDegrees = Math.Clamp(PitchDegrees + dy, _cfg.MinPitchDegrees, _cfg.MaxPitchDegrees);
            if (facingDegrees is { } f) AutoFollow(f, mouseMoved, dt);
        }

        var target = _world.ToWorld(playerPosition, _cfg.TargetHeight);
        float yaw = YawDegrees * MathF.PI / 180f, pitch = PitchDegrees * MathF.PI / 180f;
        var back = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
        var position = target + back * _cfg.Distance;
        float fov = _cfg.FovDegrees;

        if (ShoulderBlend > 0 && _lastWork is { } focus)
        {
            // Smoothstep, so the camera eases in and out.
            float t = ShoulderBlend * ShoulderBlend * (3 - 2 * ShoulderBlend);
            var elf = _world.ToWorld(playerPosition);
            var right = new Vector3(-focus.Direction.Z, 0, focus.Direction.X);
            var shoulder = elf - focus.Direction * _shoulder.Back + right * _shoulder.Side + new Vector3(0, _shoulder.Height, 0);
            var look = focus.Spot + new Vector3(0, _shoulder.LookAbove, 0);
            position = Vector3.Lerp(position, shoulder, t);
            target = Vector3.Lerp(target, look, t);
            fov += (_shoulder.FovDegrees - fov) * t;
        }

        // Keep the camera inside the room so walls never block the view of the elf.
        var min = _world.ToWorld(new Point2(_floor.Left, _floor.Top));
        var max = _world.ToWorld(new Point2(_floor.Right, _floor.Bottom));
        float m = _cfg.WallMargin;
        position.X = Math.Clamp(position.X, min.X + m, max.X - m);
        position.Z = Math.Clamp(position.Z, min.Z + m, max.Z - m);
        position.Y = Math.Clamp(position.Y, 0.3f, _cfg.MaxHeight); // stay under the ceiling beams

        _world.Camera = new Camera3D
        {
            Position = position,
            Target = target,
            Up = Vector3.UnitY,
            FovY = fov,
            Projection = CameraProjection.Perspective,
        };
    }

    private WorkFocus? _lastWork;

    // Auto-follow: how long the elf has kept facing roughly one way, and which way.
    private float _steadyTime;
    private float _steadyFacing;
    private const float SteadyToleranceDegrees = 12;

    // Movement keys are read relative to the camera as it was when they were pressed (or when the mouse last moved),
    // so an auto-follow swing never bends the elf's path: holding one key keeps walking the same way.
    private Vector2 _lastInput;
    private float _inputYaw;
    private bool _mouseMovedSinceInput = true;

    private void AutoFollow(float facing, bool mouseMoved, float dt)
    {
        if (_cfg.AutoFollowSeconds <= 0) return;
        if (mouseMoved || MathF.Abs(DeltaDegrees(facing, _steadyFacing)) > SteadyToleranceDegrees)
        {
            _steadyFacing = facing;
            _steadyTime = 0;
            return;
        }
        _steadyTime += dt;
        if (_steadyTime < _cfg.AutoFollowSeconds) return;

        // Behind the elf: the camera looks the way it faces (camera forward = (-sin yaw, -cos yaw)).
        float behind = facing + 180;
        float ease = 1 - MathF.Exp(-_cfg.AutoFollowSpeed * dt);
        YawDegrees += DeltaDegrees(behind, YawDegrees) * ease;
    }

    private static float DeltaDegrees(float to, float from)
    {
        float d = (to - from) % 360;
        if (d > 180) d -= 360;
        if (d < -180) d += 360;
        return d;
    }

    /// <summary>
    /// Turns key input (x: right, y: -1 = forward) into a direction on the floor, relative to the camera.
    /// Floor x matches world X and floor y matches world Z.
    /// </summary>
    public (double X, double Y) ToFloorDirection(Vector2 input)
    {
        if (input != _lastInput || _mouseMovedSinceInput)
        {
            _inputYaw = YawDegrees;
            _lastInput = input;
            _mouseMovedSinceInput = false;
        }
        float yaw = _inputYaw * MathF.PI / 180f;
        var forward = new Vector2(-MathF.Sin(yaw), -MathF.Cos(yaw));
        var right = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
        var dir = right * input.X + forward * -input.Y;
        if (dir.LengthSquared() > 1) dir = Vector2.Normalize(dir);
        return (dir.X, dir.Y);
    }
}
