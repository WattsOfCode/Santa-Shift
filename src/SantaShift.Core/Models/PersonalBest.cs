namespace SantaShift.Core.Models;

/// <summary>Best scores per mode, saved to best.json in the save folder.</summary>
public sealed class PersonalBest
{
    public const int CurrentSchema = 1;

    public int SchemaVersion { get; set; } = CurrentSchema;
    public int StandardScore { get; set; }
    public int RelaxedScore { get; set; }

    public int For(GameMode mode) => mode == GameMode.Relaxed ? RelaxedScore : StandardScore;

    /// <summary>Records a score; returns true if it is a new best.</summary>
    public bool Submit(GameMode mode, int score)
    {
        if (score <= For(mode)) return false;
        if (mode == GameMode.Relaxed) RelaxedScore = score; else StandardScore = score;
        return true;
    }
}
