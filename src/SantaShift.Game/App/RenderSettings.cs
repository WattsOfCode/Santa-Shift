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
    public ShoulderCameraSettings ShoulderCamera { get; init; } = new();
    public MovementSettings Movement { get; init; } = new();
    public MenuCameraSettings MenuCamera { get; init; } = new();
    public LightingSettings Lighting { get; init; } = new();

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

    /// <summary>After the elf has faced one way this long (and the mouse is left alone), the camera swings in behind it. 0 = never.</summary>
    public float AutoFollowSeconds { get; init; } = 2.0f;

    /// <summary>How quickly it swings round (higher = faster).</summary>
    public float AutoFollowSpeed { get; init; } = 2.5f;
}

/// <summary>How the elf turns and gets going (feel only: the rules still decide where it can walk).</summary>
public sealed class MovementSettings
{
    public float TurnDegreesPerSecond { get; init; } = 720;

    /// <summary>Time to reach full speed from standing.</summary>
    public float AccelerationSeconds { get; init; } = 0.12f;

    /// <summary>Slowest speed (fraction of full) while coming round a sharp turn.</summary>
    public float TurningSpeedFloor { get; init; } = 0.2f;
}

/// <summary>Close view over the elf's right shoulder while it builds a toy at the bench.</summary>
public sealed class ShoulderCameraSettings
{
    /// <summary>How far behind the elf, along its line of sight to the work.</summary>
    public float Back { get; init; } = 0.85f;

    /// <summary>To the elf's right.</summary>
    public float Side { get; init; } = 0.75f;

    public float Height { get; init; } = 1.7f;

    /// <summary>Looks this far above the bench top, at the toy.</summary>
    public float LookAbove { get; init; } = 0.12f;

    public float FovDegrees { get; init; } = 55;

    /// <summary>Time to swing in and out of the close view.</summary>
    public float BlendSeconds { get; init; } = 0.35f;
}

public sealed class MenuCameraSettings
{
    public Vec3 Position { get; init; } = new() { X = 0, Y = 2.8f, Z = 4.0f };
    public Vec3 Target { get; init; } = new() { X = 0, Y = 0.8f, Z = -1.5f };
    public float FovDegrees { get; init; } = 60;
}

/// <summary>The lighting look and its cost. Quality: "high" (16 lights), "medium" (8), "low" (4, no haze or post effects).</summary>
public sealed class LightingSettings
{
    public string Quality { get; init; } = "high";

    /// <summary>Overrides the quality's light count when above 0.</summary>
    public int MaxLights { get; init; }

    public float Exposure { get; init; } = 2.0f;
    public float EmissiveBoost { get; init; } = 1.3f;
    public float[] AmbientSky { get; init; } = [0.13f, 0.13f, 0.18f];
    public float[] AmbientGround { get; init; } = [0.19f, 0.12f, 0.08f];
    public Vec3 DirectionalDirection { get; init; } = new() { X = -0.35f, Y = -1f, Z = -0.45f };
    public float[] DirectionalColor { get; init; } = [0.10f, 0.11f, 0.15f];
    public float[] FogColor { get; init; } = [0.05f, 0.03f, 0.02f];
    public float FogDensity { get; init; } = 0.018f;
    public float Vignette { get; init; } = 0.45f;
    public float Saturation { get; init; } = 1.15f;

    /// <summary>A soft light that follows the elf so the player stays readable in dark corners (0 = off).</summary>
    public float PlayerFill { get; init; } = 0.45f;
    public float[] PlayerFillColor { get; init; } = [1.0f, 0.85f, 0.7f];

    /// <summary>Used when the room has no lights (the built-in grey-box room).</summary>
    public float[] FallbackAmbient { get; init; } = [0.58f, 0.56f, 0.54f];
    public float[] FallbackDirectionalColor { get; init; } = [0.62f, 0.52f, 0.40f];

    public int MaxLightsForQuality() => MaxLights > 0 ? MaxLights : Quality.ToLowerInvariant() switch
    {
        "low" => 4,
        "medium" => 8,
        _ => 16,
    };

    public bool FogForQuality() => !Quality.Equals("low", StringComparison.OrdinalIgnoreCase);
    public bool PostEffectsForQuality() => !Quality.Equals("low", StringComparison.OrdinalIgnoreCase);
}

public sealed class Vec3
{
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public System.Numerics.Vector3 ToVector() => new(X, Y, Z);
}
