namespace SantaShift.Core.Models;

/// <summary>Best runs per mode (total score and furthest round), saved to best.json in the save folder.</summary>
public sealed class PersonalBest
{
    public const int CurrentSchema = 1;

    public int SchemaVersion { get; set; } = CurrentSchema;
    public int StandardScore { get; set; }
    public int RelaxedScore { get; set; }
    public int StandardRound { get; set; }
    public int RelaxedRound { get; set; }

    public int For(GameMode mode) => mode == GameMode.Relaxed ? RelaxedScore : StandardScore;
    public int RoundFor(GameMode mode) => mode == GameMode.Relaxed ? RelaxedRound : StandardRound;

    /// <summary>Records a run's total score; returns true if it is a new best score.</summary>
    public bool Submit(GameMode mode, int score, int round = 0)
    {
        if (round > RoundFor(mode))
        {
            if (mode == GameMode.Relaxed) RelaxedRound = round; else StandardRound = round;
        }
        if (score <= For(mode)) return false;
        if (mode == GameMode.Relaxed) RelaxedScore = score; else StandardScore = score;
        return true;
    }
}
