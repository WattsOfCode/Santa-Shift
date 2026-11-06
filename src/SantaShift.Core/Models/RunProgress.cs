namespace SantaShift.Core.Models;

/// <summary>
/// A whole run: a series of rounds (shifts). Meeting a round's quota starts the next, harder round;
/// missing it is game over. Score carries across rounds.
/// </summary>
public sealed class RunProgress(GameMode mode)
{
    public GameMode Mode { get; } = mode;

    /// <summary>The round being played, or the last one played once the run is over.</summary>
    public int Round { get; private set; } = 1;

    public int TotalScore { get; private set; }
    public int TotalDelivered { get; private set; }
    public int RoundsCleared { get; private set; }
    public bool IsOver { get; private set; }

    /// <summary>Records a finished round. Returns true if it was passed (the run continues).</summary>
    public bool CompleteRound(RunState round, int quota)
    {
        if (IsOver) throw new InvalidOperationException("The run is already over.");
        TotalScore += round.Score;
        TotalDelivered += round.Delivered;

        bool passed = round.Delivered >= quota;
        if (passed) RoundsCleared++;
        else IsOver = true;
        return passed;
    }

    /// <summary>Moves on to the next round. Only valid after a passed round.</summary>
    public void StartNextRound()
    {
        if (IsOver) throw new InvalidOperationException("The run is over; start a new one.");
        Round++;
    }
}
