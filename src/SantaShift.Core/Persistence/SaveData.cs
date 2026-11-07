using SantaShift.Core.Models;

namespace SantaShift.Core.Persistence;

/// <summary>Loads and saves settings and best scores, with validation (GDD section 19).</summary>
public sealed class SaveData(ISaveStore store)
{
    public const string SettingsName = "settings";
    public const string BestName = "best";
    public const string ProgressName = "progress";

    public Settings LoadSettings()
    {
        var s = store.Load<Settings>(SettingsName);
        return s == null || s.SchemaVersion != Settings.CurrentSchema ? new Settings() : s.Clamp();
    }

    public bool SaveSettings(Settings s) => store.Save(SettingsName, s);

    public PersonalBest LoadBest()
    {
        var b = store.Load<PersonalBest>(BestName);
        if (b == null || b.SchemaVersion != PersonalBest.CurrentSchema) return new PersonalBest();
        b.StandardScore = Math.Max(0, b.StandardScore);
        b.RelaxedScore = Math.Max(0, b.RelaxedScore);
        return b;
    }

    public bool SaveBest(PersonalBest b) => store.Save(BestName, b);

    /// <summary>Lifetime totals and unlocks. A missing, broken or old file starts fresh.</summary>
    public PlayerProgress LoadProgress()
    {
        var p = store.Load<PlayerProgress>(ProgressName);
        return p == null || p.SchemaVersion != PlayerProgress.CurrentSchema ? new PlayerProgress() : p.Clamp();
    }

    public bool SaveProgress(PlayerProgress p) => store.Save(ProgressName, p);
}
