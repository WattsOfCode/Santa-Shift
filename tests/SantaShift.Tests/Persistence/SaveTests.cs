using SantaShift.Core.Models;
using SantaShift.Core.Persistence;

namespace SantaShift.Tests.Persistence;

/// <summary>Storage must never stop the game (GDD sections 19 and 24).</summary>
public class SaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "santashift-save-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        if (File.Exists(_dir)) File.Delete(_dir);
    }

    [Fact]
    public void Settings_round_trip()
    {
        var data = new SaveData(new JsonFileStore(_dir));
        data.SaveSettings(new Settings { MusicVolume = 0.4f, ReducedMotion = true, PreferredMode = GameMode.Relaxed });

        var loaded = data.LoadSettings();

        Assert.Equal(0.4f, loaded.MusicVolume);
        Assert.True(loaded.ReducedMotion);
        Assert.Equal(GameMode.Relaxed, loaded.PreferredMode);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var loaded = new SaveData(new JsonFileStore(_dir)).LoadSettings();
        Assert.Equal(0.8f, loaded.MasterVolume);
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ nope");

        Assert.Equal(new Settings().EffectsVolume, new SaveData(new JsonFileStore(_dir)).LoadSettings().EffectsVolume);
    }

    [Fact]
    public void Unknown_schema_version_gives_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "schemaVersion": 99, "musicVolume": 0.9 }""");

        Assert.Equal(0.25f, new SaveData(new JsonFileStore(_dir)).LoadSettings().MusicVolume);
    }

    [Fact]
    public void Out_of_range_volumes_are_clamped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "schemaVersion": 1, "masterVolume": 7, "musicVolume": -2 }""");

        var s = new SaveData(new JsonFileStore(_dir)).LoadSettings();
        Assert.Equal(1f, s.MasterVolume);
        Assert.Equal(0f, s.MusicVolume);
    }

    [Fact]
    public void Blocked_writes_return_false_instead_of_throwing()
    {
        File.WriteAllText(_dir, "a file where the folder should be");
        var store = new JsonFileStore(_dir);

        Assert.False(store.Save("settings", new Settings()));
        Assert.Null(store.Load<Settings>("settings"));
    }

    [Fact]
    public void Memory_store_keeps_copies()
    {
        var store = new MemoryStore();
        var best = new PersonalBest { StandardScore = 10 };
        store.Save("best", best);
        best.StandardScore = 999;

        Assert.Equal(10, store.Load<PersonalBest>("best")!.StandardScore);
    }

    [Fact]
    public void Negative_best_scores_are_corrected()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "best.json"), """{ "schemaVersion": 1, "standardScore": -50, "relaxedScore": 20 }""");

        var b = new SaveData(new JsonFileStore(_dir)).LoadBest();
        Assert.Equal(0, b.StandardScore);
        Assert.Equal(20, b.RelaxedScore);
    }
}
