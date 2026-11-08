using Raylib_cs;
using SantaShift.Core.Models;

namespace SantaShift.Game.Services;

/// <summary>
/// Sound IDs from GDD section 17. Each plays Content/audio/&lt;id&gt;.wav if that file exists. The shipped files are
/// synthesised by tools/audio/make_sfx.py; replace any of them with your own of the same name.
/// </summary>
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
    public const string PunchClock = "punch_clock";
    public const string UiMove = "ui_move";
    public const string RibbonPick = "ribbon_pick";
    public const string CoalShovel = "coal_shovel";
    public const string CratePickup = "crate_pickup";
    public const string BinRestock = "bin_restock";
    public const string CoalDelivered = "coal_delivered";
    public const string Recycle = "recycle";
    public const string OrderNew = "order_new";
    public const string ClockTick = "clock_tick";
    public const string RoundClear = "round_clear";
    public const string RunOver = "run_over";
    public const string PerkPick = "perk_pick";
    public const string Unlock = "unlock";
    public const string JollyOn = "jolly_on";
    public const string SpiritLow = "spirit_low";

    /// <summary>Footstep variants, picked at random so walking doesn't sound like a loop.</summary>
    public static readonly string[] Footsteps = ["footstep_1", "footstep_2", "footstep_3", "footstep_4"];

    public static readonly string[] All =
        [UiConfirm, KitPickup, AssemblyComplete, RibbonComplete, DeliverySuccess, ActionRejected, PolicyChime, OrderExpired, ShiftEnd,
         HammerHit, AssemblyBotched, PunchClock, UiMove, RibbonPick, CoalShovel, CratePickup, BinRestock, CoalDelivered, Recycle,
         OrderNew, ClockTick, RoundClear, RunOver, PerkPick, Unlock, JollyOn, SpiritLow, .. Footsteps];
}

/// <summary>Music tracks. Each streams Content/audio/&lt;id&gt;.mp3 (or .ogg / .wav) if that file exists, looping.</summary>
public static class MusicIds
{
    /// <summary>Title, Santa's list and results: "Cozy Christmas Game Ambience" by blueeyedmonkey.</summary>
    public const string Menu = "music_menu";

    /// <summary>During a shift: "Toy Factory Jingle" by blueeyedmonkey.</summary>
    public const string Workshop = "music_workshop";

    public static readonly string[] All = [Menu, Workshop];

    /// <summary>The hearth's crackle (Content/audio/fire_loop.wav), streamed so it loops without a gap; an effect, not music.</summary>
    public const string FireLoop = "fire_loop";
}

/// <summary>Plays sound effects. Swappable; the game never depends on sound working (GDD section 14).</summary>
public interface IAudioService : IDisposable
{
    /// <param name="pitch">1 = as recorded; 1.1 plays 10% higher (and shorter).</param>
    void Play(string soundId, float pitch = 1f);
    void ApplySettings(Settings settings);

    /// <summary>Call once per frame: fades towards <paramref name="musicId"/> (null = silence) and keeps the stream fed.</summary>
    void UpdateMusic(string? musicId, float dt);

    /// <summary>Call once per frame: eases the fireplace loop towards <paramref name="volume"/> (0..1 of the effects volume).</summary>
    void UpdateFire(float volume, float dt);
}

/// <summary>Used when there is no audio device. Does nothing, safely.</summary>
public sealed class SilentAudioService : IAudioService
{
    public void Play(string soundId, float pitch = 1f) { }
    public void ApplySettings(Settings settings) { }
    public void UpdateMusic(string? musicId, float dt) { }
    public void UpdateFire(float volume, float dt) { }
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

    private Music? _fire;
    private float _fireLevel; // eased towards the wanted volume, so walking past (or pausing) never jumps

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
        string firePath = Path.Combine(audioDirectory, MusicIds.FireLoop + ".wav");
        if (File.Exists(firePath))
        {
            var fire = Raylib.LoadMusicStream(firePath);
            if (Raylib.IsMusicValid(fire)) { fire.Looping = true; service._fire = fire; }
        }
        return service;
    }

    public void Play(string soundId, float pitch = 1f)
    {
        if (!_sounds.TryGetValue(soundId, out var sound)) return;
        double now = Raylib.GetTime();
        if (_lastPlayed.TryGetValue(soundId, out double last) && now - last < MinSecondsBetweenRepeats) return;
        _lastPlayed[soundId] = now;
        Raylib.SetSoundVolume(sound, _effectsVolume);
        Raylib.SetSoundPitch(sound, pitch);
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

    public void UpdateFire(float volume, float dt)
    {
        if (_fire is not { } fire) return;
        float target = Math.Clamp(volume, 0f, 1f);
        float rate = dt / 0.6f; // about 0.6 s from silent to full
        _fireLevel = target > _fireLevel ? Math.Min(target, _fireLevel + rate) : Math.Max(target, _fireLevel - rate);
        bool playing = Raylib.IsMusicStreamPlaying(fire);
        if (_fireLevel <= 0f)
        {
            if (playing) Raylib.PauseMusicStream(fire);
            return;
        }
        if (!playing) Raylib.PlayMusicStream(fire);
        Raylib.SetMusicVolume(fire, _effectsVolume * _fireLevel);
        Raylib.UpdateMusicStream(fire);
    }

    public void Dispose()
    {
        foreach (var sound in _sounds.Values) Raylib.UnloadSound(sound);
        _sounds.Clear();
        foreach (var music in _music.Values) Raylib.UnloadMusicStream(music);
        _music.Clear();
        if (_fire is { } fire) Raylib.UnloadMusicStream(fire);
        _fire = null;
        Raylib.CloseAudioDevice();
    }
}
