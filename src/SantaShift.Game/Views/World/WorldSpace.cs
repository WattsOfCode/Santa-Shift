using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Game.App;

namespace SantaShift.Game.Views.World;

/// <summary>
/// Converts between the rules' flat floor (logical pixels) and the 3D world (metres), and holds the active camera.
/// The world origin and scale come from tuning.json (worldOrigin, pixelsPerMetre), the same values the
/// Blender room export uses. +X is right, +Z is towards the player's starting side, +Y is up.
/// </summary>
public sealed class WorldSpace
{
    private readonly float _ppm;
    private readonly Point2 _origin;
    private readonly MenuCameraSettings _menu;

    /// <summary>The camera views draw with. The workshop's camera controller updates it every frame.</summary>
    public Camera3D Camera { get; set; }

    public int TargetWidth { get; private set; }
    public int TargetHeight { get; private set; }

    /// <summary>The 3D picture changed size (it follows the window).</summary>
    public void Resize(int width, int height) => (TargetWidth, TargetHeight) = (width, height);

    public WorldSpace(RenderSettings render, Tuning tuning)
    {
        _ppm = (float)tuning.PixelsPerMetre;
        _origin = tuning.WorldOrigin;
        _menu = render.MenuCamera;
        TargetWidth = render.InternalWidth;
        TargetHeight = render.InternalHeight;
        UseMenuCamera();
    }

    /// <summary>The fixed view used behind menus.</summary>
    public void UseMenuCamera() => Camera = new Camera3D
    {
        Position = _menu.Position.ToVector(),
        Target = _menu.Target.ToVector(),
        Up = Vector3.UnitY,
        FovY = _menu.FovDegrees,
        Projection = CameraProjection.Perspective,
    };

    public Vector3 ToWorld(Point2 p, float height = 0) =>
        new((float)(p.X - _origin.X) / _ppm, height, (float)(p.Y - _origin.Y) / _ppm);

    public float Metres(double pixels) => (float)pixels / _ppm;

    /// <summary>True if a 3D point is in front of the camera (so it can be labelled on screen).</summary>
    public bool InFront(Vector3 world)
    {
        var forward = Vector3.Normalize(Camera.Target - Camera.Position);
        return Vector3.Dot(world - Camera.Position, forward) > 0.1f;
    }

    public float DistanceFromCamera(Vector3 world) => Vector3.Distance(world, Camera.Position);

    /// <summary>Where a 3D point appears in logical UI coordinates (960 x 540), for labels and progress bars.</summary>
    public Vector2 ToUi(Vector3 world)
    {
        var s = Raylib.GetWorldToScreenEx(world, Camera, TargetWidth, TargetHeight);
        return new Vector2(s.X * 960f / TargetWidth, s.Y * 540f / TargetHeight);
    }
}
