using Raylib_cs;
using SantaShift.Core.Models;

namespace SantaShift.Game.Services;

/// <summary>Sound IDs from GDD section 17. Each plays Content/audio/&lt;id&gt;.wav if that file exists.</summary>
public static class SoundIds
{
    public const string UiConfirm = "ui_confirm";
    public const string KitPickup = "kit_pickup";
    public const string AssemblyComplete = "assembly_complete";
    public const string RibbonComplete = "ribbon_complete";
    public const string DeliverySuccess = "delivery_success";
    public const string ActionRejected = "action_rejected";
    public const string PolicyChime = "policy_chime";
    public const string OrderExpired = "order_expired";
    public const string ShiftEnd = "shift_end";

    public static readonly string[] All =
        [UiConfirm, KitPickup, AssemblyComplete, RibbonComplete, DeliverySuccess, ActionRejected, PolicyChime, OrderExpired, ShiftEnd];
}

/// <summary>Plays sound effects. Swappable; the game never depends on sound working (GDD section 14).</summary>
public interface IAudioService : IDisposable
{
    void Play(string soundId);
    void ApplySettings(Settings settings);
}

/// <summary>Used when there is no audio device. Does nothing, safely.</summary>
public sealed class SilentAudioService : IAudioService
{
    public void Play(string soundId) { }
    public void ApplySettings(Settings settings) { }
    public void Dispose() { }
}

public sealed class RaylibAudioService : IAudioService
{
    private const double MinSecondsBetweenRepeats = 0.1; // stops rapid rejected-action spam (GDD section 17)

    private readonly Dictionary<string, Sound> _sounds = new();
    private readonly Dictionary<string, double> _lastPlayed = new();
    private float _effectsVolume = 0.7f;

    private RaylibAudioService() { }

    /// <summary>Opens the audio device, or returns a silent service if that fails.</summary>
    public static IAudioService Create(string audioDirectory)
    {
        try
        {
            Raylib.InitAudioDevice();
            if (!Raylib.IsAudioDeviceReady()) return new SilentAudioService();
        }
        catch (Exception) { return new SilentAudioService(); }

        var service = new RaylibAudioService();
        foreach (var id in SoundIds.All)
        {
            string path = Path.Combine(audioDirectory, id + ".wav");
            if (!File.Exists(path)) continue;
            var sound = Raylib.LoadSound(path);
            if (Raylib.IsSoundValid(sound)) service._sounds[id] = sound;
        }
        return service;
    }

    public void Play(string soundId)
    {
        if (!_sounds.TryGetValue(soundId, out var sound)) return;
        double now = Raylib.GetTime();
        if (_lastPlayed.TryGetValue(soundId, out double last) && now - last < MinSecondsBetweenRepeats) return;
        _lastPlayed[soundId] = now;
        Raylib.SetSoundVolume(sound, _effectsVolume);
        Raylib.PlaySound(sound);
    }

    public void ApplySettings(Settings settings)
    {
        Raylib.SetMasterVolume(settings.Muted ? 0 : settings.MasterVolume);
        _effectsVolume = settings.EffectsVolume;
    }

    public void Dispose()
    {
        foreach (var sound in _sounds.Values) Raylib.UnloadSound(sound);
        _sounds.Clear();
        Raylib.CloseAudioDevice();
    }
}
