using System.Text.Json;
using SantaShift.Core.Content;

namespace SantaShift.Game.App;

/// <summary>Window and camera settings, loaded from Content/data/render.json.</summary>
public sealed class RenderSettings
{
    public int InternalWidth { get; init; } = 960;
    public int InternalHeight { get; init; } = 540;
    public int WindowWidth { get; init; } = 1280;
    public int WindowHeight { get; init; } = 720;
    public bool Vsync { get; init; } = true;
    public int TargetFps { get; init; } = 60;
    public bool Msaa { get; init; }
    public ChaseCameraSettings ChaseCamera { get; init; } = new();
    public MenuCameraSettings MenuCamera { get; init; } = new();

    public static RenderSettings Load(string dataDirectory)
    {
        string path = Path.Combine(dataDirectory, "render.json");
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<RenderSettings>(File.ReadAllText(path), JsonDefaults.Options) ?? new()
                : new();
        }
        catch (JsonException) { return new(); }
    }
}

public sealed class ChaseCameraSettings
{
    public float Distance { get; init; } = 3.6f;
    public float TargetHeight { get; init; } = 1.1f;
    public float PitchDegrees { get; init; } = 22;
    public float MinPitchDegrees { get; init; } = 5;
    public float MaxPitchDegrees { get; init; } = 60;
    public float FovDegrees { get; init; } = 60;
    public float MouseSensitivity { get; init; } = 0.15f;
    public bool InvertY { get; init; }
    public float WallMargin { get; init; } = 0.25f;
    public float MaxHeight { get; init; } = 3.0f;
}

public sealed class MenuCameraSettings
{
    public Vec3 Position { get; init; } = new() { X = 0, Y = 2.8f, Z = 4.0f };
    public Vec3 Target { get; init; } = new() { X = 0, Y = 0.8f, Z = -1.5f };
    public float FovDegrees { get; init; } = 60;
}

public sealed class Vec3
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public System.Numerics.Vector3 ToVector() => new(X, Y, Z);
}
