using SantaShift.Core.Content;

namespace SantaShift.Core.Models;

/// <summary>
/// A whole run: a series of rounds (shifts). Meeting a round's quota starts the next, harder round;
/// missing it is game over. Score carries across rounds.
/// </summary>
public sealed class RunProgress(GameMode mode, NaughtyNiceList? list = null)
{
    public GameMode Mode { get; } = mode;

    /// <summary>Santa's list for this run, drawn when it starts and kept for every round.</summary>
    public NaughtyNiceList List { get; } = list ?? NaughtyNiceList.Empty;

    /// <summary>A fresh run with a newly drawn naughty and nice list.</summary>
    public static RunProgress Start(GameMode mode, IEnumerable<ChildLetter> letters, int naughtyCount, int seed) =>
        new(mode, NaughtyNiceList.Draw(letters, naughtyCount, seed));

    /// <summary>The round being played, or the last one played once the run is over.</summary>
    public int Round { get; private set; } = 1;

    private readonly List<Perk> _perks = [];

    /// <summary>The perks picked so far, in order (a perk picked twice appears twice).</summary>
    public IReadOnlyList<Perk> Perks => _perks;

    /// <summary>All picked perks combined, for the next round's simulation.</summary>
    public RunModifiers Modifiers => RunModifiers.From(_perks);

    /// <summary>How far over the quota the last passed round was (a good round earns an extra perk card).</summary>
    public int LastMargin { get; private set; }

    private double? _spirit;

    /// <summary>
    /// The Christmas Spirit to start the next round with: what the last round ended on plus a night's rest, or the
    /// starting amount for the first round.
    /// </summary>
    public double SpiritForNextRound(SpiritTuning t) =>
        _spirit is { } s ? Math.Min(t.Max, s + t.BetweenRounds) : t.Start;

    public int Stacks(string perkId) => _perks.Count(p => p.Id == perkId);

    public void AddPerk(Perk perk)
    {
        if (Stacks(perk.Id) >= perk.MaxStacks) throw new InvalidOperationException($"{perk.Id} is already at its limit.");
        _perks.Add(perk);
    }

    /// <summary>
    /// The perk cards Santa offers after a passed round: different perks, none already at its limit, drawn with
    /// <paramref name="seed"/>. One extra card when the round beat the quota by <paramref name="bonusMargin"/> or more.
    /// </summary>
    public IReadOnlyList<Perk> OfferPerks(IReadOnlyList<Perk> pool, int cards, int bonusMargin, int seed)
    {
        int count = cards + (LastMargin >= bonusMargin ? 1 : 0);
        var random = new Random(seed);
        return pool.Where(p => Stacks(p.Id) < p.MaxStacks).OrderBy(_ => random.Next()).Take(count).ToList();
    }

    /// <summary>Toy cart parts built so far this run: one more for each passed round (see <see cref="CartTuning"/>).</summary>
    public int CartParts { get; private set; }

    /// <summary>The cart part the last passed round added, or null (none earned, or the cart was already complete).</summary>
    public string? NewCartPart { get; private set; }

    public int TotalScore { get; private set; }
    public int TotalDelivered { get; private set; }
    public int RoundsCleared { get; private set; }
    public bool IsOver { get; private set; }

    /// <summary>
    /// Records a finished round. Returns true if it was passed (the run continues). A passed round also adds the next toy
    /// cart part, when <paramref name="cart"/> is given and the cart isn't complete yet.
    /// </summary>
    public bool CompleteRound(RunState round, int quota, CartTuning? cart = null)
    {
        if (IsOver) throw new InvalidOperationException("The run is already over.");
        TotalScore += round.Score;
        TotalDelivered += round.Delivered;

        bool passed = round.Delivered >= quota && !round.SpiritOut;
        _spirit = round.Spirit;
        NewCartPart = null;
        if (passed && cart != null && CartParts < cart.Parts.Length) NewCartPart = cart.Parts[CartParts++];
        if (passed) RoundsCleared++;
        else IsOver = true;
        LastMargin = passed ? round.Delivered - quota : 0;
        return passed;
    }

    /// <summary>Moves on to the next round. Only valid after a passed round.</summary>
    public void StartNextRound()
    {
        if (IsOver) throw new InvalidOperationException("The run is over; start a new one.");
        Round++;
    }
}
