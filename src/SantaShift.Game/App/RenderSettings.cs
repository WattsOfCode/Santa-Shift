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
    public CameraSettings Camera { get; init; } = new();
    public float PixelsPerMetre { get; init; } = 40;

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

public sealed class CameraSettings
{
    public float Distance { get; init; } = 15.5f;
    public float PitchDegrees { get; init; } = 60;
    public float FovDegrees { get; init; } = 45;
    public float TargetZ { get; init; } = 0.9f;
}
