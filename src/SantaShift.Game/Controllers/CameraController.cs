using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

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

    public CameraController(ChaseCameraSettings cfg, WorldSpace world, GameContent content)
    {
        _cfg = cfg;
        _world = world;
        _floor = content.Floor;
        PitchDegrees = cfg.PitchDegrees;
    }

    /// <summary>Applies mouse movement and repositions the camera behind the elf. Call once per frame.</summary>
    public void Update(Vector2 mouseDelta, Point2 playerPosition)
    {
        YawDegrees -= mouseDelta.X * _cfg.MouseSensitivity;
        float dy = mouseDelta.Y * _cfg.MouseSensitivity * (_cfg.InvertY ? -1 : 1);
        PitchDegrees = Math.Clamp(PitchDegrees + dy, _cfg.MinPitchDegrees, _cfg.MaxPitchDegrees);

        var target = _world.ToWorld(playerPosition, _cfg.TargetHeight);
        float yaw = YawDegrees * MathF.PI / 180f, pitch = PitchDegrees * MathF.PI / 180f;
        var back = new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
        var position = target + back * _cfg.Distance;

        // Keep the camera inside the room so walls never block the view of the elf.
        var min = _world.ToWorld(new Point2(_floor.Left, _floor.Top));
        var max = _world.ToWorld(new Point2(_floor.Right, _floor.Bottom));
        float m = _cfg.WallMargin;
        position.X = Math.Clamp(position.X, min.X + m, max.X - m);
        position.Z = Math.Clamp(position.Z, min.Z + m, max.Z - m);
        position.Y = Math.Max(position.Y, 0.3f);

        _world.Camera = new Camera3D
        {
            Position = position,
            Target = target,
            Up = Vector3.UnitY,
            FovY = _cfg.FovDegrees,
            Projection = CameraProjection.Perspective,
        };
    }

    /// <summary>
    /// Turns key input (x: right, y: -1 = forward) into a direction on the floor, relative to the camera.
    /// Floor x matches world X and floor y matches world Z.
    /// </summary>
    public (double X, double Y) ToFloorDirection(Vector2 input)
    {
        float yaw = YawDegrees * MathF.PI / 180f;
        var forward = new Vector2(-MathF.Sin(yaw), -MathF.Cos(yaw));
        var right = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
        var dir = right * input.X + forward * -input.Y;
        if (dir.LengthSquared() > 1) dir = Vector2.Normalize(dir);
        return (dir.X, dir.Y);
    }
}
