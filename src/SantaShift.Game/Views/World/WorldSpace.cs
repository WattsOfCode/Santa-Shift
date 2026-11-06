using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.App;

namespace SantaShift.Game.Views.World;

/// <summary>
/// Converts between the rules' flat floor (logical pixels) and the 3D world (metres), and owns the fixed camera.
/// Floor centre (480, 300) px is the world origin; +X is right, +Z is towards the camera, +Y is up.
/// </summary>
public sealed class WorldSpace
{
    private const float CentreX = 480, CentreY = 300;

    private readonly float _ppm;

    public Camera3D Camera { get; }
    public int TargetWidth { get; }
    public int TargetHeight { get; }

    public WorldSpace(RenderSettings render)
    {
        _ppm = render.PixelsPerMetre;
        TargetWidth = render.InternalWidth;
        TargetHeight = render.InternalHeight;

        var c = render.Camera;
        float pitch = c.PitchDegrees * MathF.PI / 180f;
        var target = new Vector3(0, 0, c.TargetZ);
        var position = target + new Vector3(0, c.Distance * MathF.Sin(pitch), c.Distance * MathF.Cos(pitch));
        Camera = new Camera3D
        {
            Position = position,
            Target = target,
            Up = Vector3.UnitY,
            FovY = c.FovDegrees,
            Projection = CameraProjection.Perspective,
        };
    }

    public Vector3 ToWorld(Point2 p, float height = 0) =>
        new((float)(p.X - CentreX) / _ppm, height, (float)(p.Y - CentreY) / _ppm);

    public float Metres(double pixels) => (float)pixels / _ppm;

    /// <summary>Where a 3D point appears in logical UI coordinates (960 x 540), for labels and progress bars.</summary>
    public Vector2 ToUi(Vector3 world)
    {
        var s = Raylib.GetWorldToScreenEx(world, Camera, TargetWidth, TargetHeight);
        return new Vector2(s.X * 960f / TargetWidth, s.Y * 540f / TargetHeight);
    }
}
