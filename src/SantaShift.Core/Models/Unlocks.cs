namespace SantaShift.Core.Models;

/// <summary>The lifetime totals an unlock can be earned with.</summary>
public enum UnlockStat { ToysDelivered, CoalDelivered, BestRound, RoundsCleared, RunsPlayed }

/// <summary>What an unlock gives: a perk added to Santa's offers, a hat colour for the elf, or a perk before round 1.</summary>
public enum UnlockReward { Perk, Hat, HeadStart }

/// <summary>Something earned across runs (unlocks.json): reach <see cref="Target"/> on <see cref="Stat"/> to unlock it.</summary>
public sealed class Unlock
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public UnlockStat Stat { get; init; }
    public int Target { get; init; }
    public UnlockReward Reward { get; init; }

    /// <summary>Perk rewards: the perk id. Hat rewards: the hat colour as "#RRGGBB".</summary>
    public string Value { get; init; } = "";
}

/// <summary>
/// Progress kept between runs (saved as progress.json): lifetime totals, what has been unlocked, and the chosen hat.
/// </summary>
public sealed class PlayerProgress
{
    public const int CurrentSchema = 1;

    public int SchemaVersion { get; set; } = CurrentSchema;
    public int ToysDelivered { get; set; }
    public int CoalDelivered { get; set; }
    public int BestRound { get; set; }
    public int RoundsCleared { get; set; }
    public int RunsPlayed { get; set; }
    public List<string> Unlocked { get; set; } = [];

    /// <summary>The unlocked hat the elf wears (an unlock id), or "" for the classic red one.</summary>
    public string Hat { get; set; } = "";

    public int Value(UnlockStat stat) => stat switch
    {
        UnlockStat.ToysDelivered => ToysDelivered,
        UnlockStat.CoalDelivered => CoalDelivered,
        UnlockStat.BestRound => BestRound,
        UnlockStat.RoundsCleared => RoundsCleared,
        UnlockStat.RunsPlayed => RunsPlayed,
        _ => 0,
    };

    public bool Has(string unlockId) => Unlocked.Contains(unlockId);

    /// <summary>
    /// Adds a finished round to the totals. <paramref name="round"/> is the round number it was; a failed round also
    /// ends the run. Returns the unlocks this earned, in list order.
    /// </summary>
    public IReadOnlyList<Unlock> RecordRound(RunState state, int round, bool passed, IEnumerable<Unlock> unlocks)
    {
        ToysDelivered += state.Delivered - state.CoalDelivered;
        CoalDelivered += state.CoalDelivered;
        BestRound = Math.Max(BestRound, passed ? round + 1 : round);
        if (passed) RoundsCleared++;
        else RunsPlayed++;
        return Check(unlocks);
    }

    /// <summary>A run left from the results screen after a passed round still counts as played.</summary>
    public IReadOnlyList<Unlock> RecordRunLeft(IEnumerable<Unlock> unlocks)
    {
        RunsPlayed++;
        return Check(unlocks);
    }

    private List<Unlock> Check(IEnumerable<Unlock> unlocks)
    {
        var earned = new List<Unlock>();
        foreach (var u in unlocks)
        {
            if (Has(u.Id) || Value(u.Stat) < u.Target) continue;
            Unlocked.Add(u.Id);
            earned.Add(u);
        }
        return earned;
    }

    /// <summary>The perks Santa may offer: those no unlock holds back, plus those whose unlock has been earned.</summary>
    public IReadOnlyList<Perk> AvailablePerks(IEnumerable<Perk> perks, IEnumerable<Unlock> unlocks)
    {
        var locked = unlocks.Where(u => u.Reward == UnlockReward.Perk && !Has(u.Id)).Select(u => u.Value).ToHashSet();
        return perks.Where(p => !locked.Contains(p.Id)).ToList();
    }

    public bool HasHeadStart(IEnumerable<Unlock> unlocks) => unlocks.Any(u => u.Reward == UnlockReward.HeadStart && Has(u.Id));

    /// <summary>Fixes a hand-edited or old file.</summary>
    public PlayerProgress Clamp()
    {
        ToysDelivered = Math.Max(0, ToysDelivered);
        CoalDelivered = Math.Max(0, CoalDelivered);
        BestRound = Math.Max(0, BestRound);
        RoundsCleared = Math.Max(0, RoundsCleared);
        RunsPlayed = Math.Max(0, RunsPlayed);
        Unlocked = (Unlocked ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
        Hat ??= "";
        if (Hat != "" && !Unlocked.Contains(Hat)) Hat = "";
        return this;
    }
}
