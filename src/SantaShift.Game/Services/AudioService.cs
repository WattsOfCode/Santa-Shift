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
    public const string HammerHit = "hammer_hit";
    public const string AssemblyBotched = "assembly_botched";

    public static readonly string[] All =
        [UiConfirm, KitPickup, AssemblyComplete, RibbonComplete, DeliverySuccess, ActionRejected, PolicyChime, OrderExpired, ShiftEnd,
         HammerHit, AssemblyBotched];
}

/// <summary>Music tracks. Each streams Content/audio/&lt;id&gt;.mp3 (or .ogg / .wav) if that file exists, looping.</summary>
public static class MusicIds
{
    /// <summary>Title, Santa's list and results: "Cozy Christmas Game Ambience" by blueeyedmonkey.</summary>
    public const string Menu = "music_menu";

    /// <summary>During a shift: "Toy Factory Jingle" by blueeyedmonkey.</summary>
    public const string Workshop = "music_workshop";

    public static readonly string[] All = [Menu, Workshop];
}

/// <summary>Plays sound effects. Swappable; the game never depends on sound working (GDD section 14).</summary>
public interface IAudioService : IDisposable
{
    void Play(string soundId);
    void ApplySettings(Settings settings);

    /// <summary>Call once per frame: fades towards <paramref name="musicId"/> (null = silence) and keeps the stream fed.</summary>
    void UpdateMusic(string? musicId, float dt);
}

/// <summary>Used when there is no audio device. Does nothing, safely.</summary>
public sealed class SilentAudioService : IAudioService
{
    public void Play(string soundId) { }
    public void ApplySettings(Settings settings) { }
    public void UpdateMusic(string? musicId, float dt) { }
    public void Dispose() { }
}

public sealed class RaylibAudioService : IAudioService
{
    private const double MinSecondsBetweenRepeats = 0.1; // stops rapid rejected-action spam (GDD section 17)

    private readonly Dictionary<string, Sound> _sounds = new();
    private readonly Dictionary<string, double> _lastPlayed = new();
    private float _effectsVolume = 0.7f;
    private float _musicVolume = 0.25f;

    /// <summary>Seconds for one track to fade out and the next to fade in.</summary>
    private const float FadeSeconds = 1.2f;

    private readonly Dictionary<string, Music> _music = new();
    private readonly Dictionary<string, float> _fade = new(); // 0..1 per loaded track
    private string? _wanted;

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
        foreach (var id in MusicIds.All)
        {
            string? path = new[] { ".mp3", ".ogg", ".wav" }.Select(ext => Path.Combine(audioDirectory, id + ext)).FirstOrDefault(File.Exists);
            if (path == null) continue;
            var music = Raylib.LoadMusicStream(path);
            if (!Raylib.IsMusicValid(music)) continue;
            music.Looping = true;
            service._music[id] = music;
            service._fade[id] = 0f;
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
        _musicVolume = settings.MusicVolume;
    }

    /// <summary>
    /// Crossfades: the wanted track fades in (from the top if it had faded right out, else from where it was), the
    /// others fade out and pause. Streams are fed a little every frame, so music never loads all at once.
    /// </summary>
    public void UpdateMusic(string? musicId, float dt)
    {
        if (musicId != _wanted && musicId != null && _music.TryGetValue(musicId, out var next) && !Raylib.IsMusicStreamPlaying(next))
        {
            if (_fade[musicId] <= 0f) Raylib.PlayMusicStream(next); else Raylib.ResumeMusicStream(next);
        }
        _wanted = musicId;

        foreach (var (id, music) in _music)
        {
            float target = id == _wanted ? 1f : 0f;
            float f = _fade[id];
            f = target > f ? Math.Min(target, f + dt / FadeSeconds) : Math.Max(target, f - dt / FadeSeconds);
            _fade[id] = f;
            if (!Raylib.IsMusicStreamPlaying(music)) continue;
            if (f <= 0f && target == 0f) { Raylib.PauseMusicStream(music); continue; }
            Raylib.SetMusicVolume(music, _musicVolume * f * f); // eased, so the swap sounds even
            Raylib.UpdateMusicStream(music);
        }
    }

    public void Dispose()
    {
        foreach (var sound in _sounds.Values) Raylib.UnloadSound(sound);
        _sounds.Clear();
        foreach (var music in _music.Values) Raylib.UnloadMusicStream(music);
        _music.Clear();
        Raylib.CloseAudioDevice();
    }
}
