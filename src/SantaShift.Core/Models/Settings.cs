namespace SantaShift.Core.Models;

public enum GameMode { Standard, Relaxed }

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

    /// <summary>Fixes out-of-range values from a hand-edited or old file.</summary>
    public Settings Clamp()
    {
        MasterVolume = Math.Clamp(float.IsFinite(MasterVolume) ? MasterVolume : 0.8f, 0f, 1f);
        MusicVolume = Math.Clamp(float.IsFinite(MusicVolume) ? MusicVolume : 0.25f, 0f, 1f);
        EffectsVolume = Math.Clamp(float.IsFinite(EffectsVolume) ? EffectsVolume : 0.7f, 0f, 1f);
        if (!Enum.IsDefined(PreferredMode)) PreferredMode = GameMode.Standard;
        return this;
    }
}
