namespace SantaShift.Core.Models;

public enum GameMode { Standard, Relaxed }

/// <summary>How the camera comes round behind the elf: after it faces one way for a moment (2 s), almost at once, or never.</summary>
public enum CameraFollow { AfterAMoment, Always, Off }

/// <summary>When round 1 is guided (arrows on the floor and a lesson's worth of orders).</summary>
public enum GuideMode { UntilFirstRoundCleared, EveryRun, Off }

/// <summary>Player settings, saved to settings.json in the save folder.</summary>
public sealed class Settings
{
    public const int CurrentSchema = 1;

    public int SchemaVersion { get; set; } = CurrentSchema;
    public float MasterVolume { get; set; } = 0.8f;
    public float MusicVolume { get; set; } = 0.25f;
    public float EffectsVolume { get; set; } = 0.7f;
    public bool Muted { get; set; }
    public bool ReducedMotion { get; set; }
    public GameMode PreferredMode { get; set; } = GameMode.Standard;

    /// <summary>Keyboard and controller bindings (Settings > Controls).</summary>
    public ControlBindings Controls { get; set; } = ControlBindings.Defaults();

    /// <summary>Multiplies the mouse look speed (render.json sets the base).</summary>
    public float MouseSensitivity { get; set; } = 1f;

    /// <summary>Multiplies how fast the controller's right stick turns the camera.</summary>
    public float StickSensitivity { get; set; } = 1f;

    /// <summary>Smooth jagged edges (FXAA) on the 3D picture.</summary>
    public bool SmoothEdges { get; set; } = true;

    /// <summary>The 3D picture's resolution as a share of the window's (lower it on a weak machine).</summary>
    public float RenderScale { get; set; } = 1f;

    /// <summary>The camera swinging round behind the elf. "Always" helps on a trackpad, where mouse look is awkward.</summary>
    public CameraFollow CameraFollow { get; set; } = CameraFollow.AfterAMoment;

    /// <summary>The guided first round: until the player has cleared a round once, every run, or never.</summary>
    public GuideMode Guide { get; set; } = GuideMode.UntilFirstRoundCleared;

    /// <summary>Fixes out-of-range values from a hand-edited or old file.</summary>
    public Settings Clamp()
    {
        MasterVolume = Math.Clamp(float.IsFinite(MasterVolume) ? MasterVolume : 0.8f, 0f, 1f);
        MusicVolume = Math.Clamp(float.IsFinite(MusicVolume) ? MusicVolume : 0.25f, 0f, 1f);
        EffectsVolume = Math.Clamp(float.IsFinite(EffectsVolume) ? EffectsVolume : 0.7f, 0f, 1f);
        if (!Enum.IsDefined(PreferredMode)) PreferredMode = GameMode.Standard;
        if (!Enum.IsDefined(CameraFollow)) CameraFollow = CameraFollow.AfterAMoment;
        if (!Enum.IsDefined(Guide)) Guide = GuideMode.UntilFirstRoundCleared;
        MouseSensitivity = Math.Clamp(float.IsFinite(MouseSensitivity) ? MouseSensitivity : 1f, 0.25f, 3f);
        StickSensitivity = Math.Clamp(float.IsFinite(StickSensitivity) ? StickSensitivity : 1f, 0.25f, 3f);
        RenderScale = Math.Clamp(float.IsFinite(RenderScale) ? RenderScale : 1f, 0.5f, 1.5f);
        Controls = (Controls ?? ControlBindings.Defaults()).Normalized();
        return this;
    }
}
