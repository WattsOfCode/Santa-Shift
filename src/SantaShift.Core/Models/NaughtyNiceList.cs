namespace SantaShift.Core.Models;

/// <summary>One child on Santa's list.</summary>
public sealed record ListEntry(string Child, int Age, string ToyId, bool Naughty);

/// <summary>
/// Santa's naughty and nice list for one run: every child with a letter, with a few marked naughty.
/// Drawn at the start of each run (shown to the player) and kept for all its rounds.
/// </summary>
public sealed class NaughtyNiceList
{
    public IReadOnlyList<ListEntry> Entries { get; }
    public IReadOnlySet<string> NaughtyChildren { get; }

    public NaughtyNiceList(IEnumerable<ListEntry> entries)
    {
        Entries = entries.ToList();
        NaughtyChildren = Entries.Where(e => e.Naughty).Select(e => e.Child).ToHashSet();
    }

    public static readonly NaughtyNiceList Empty = new([]);

    /// <summary>
    /// Lists each child once (in letters.json order) and picks <paramref name="naughtyCount"/> of them at random to be
    /// naughty. The same seed always gives the same list.
    /// </summary>
    public static NaughtyNiceList Draw(IEnumerable<ChildLetter> letters, int naughtyCount, int seed)
    {
        var children = letters
            .Where(l => !string.IsNullOrWhiteSpace(l.Child))
            .GroupBy(l => l.Child)
            .Select(g => g.First())
            .ToList();

        var random = new Random(seed);
        var naughty = children.Select(c => c.Child)
            .OrderBy(_ => random.Next())
            .Take(Math.Clamp(naughtyCount, 0, children.Count))
            .ToHashSet();

        return new NaughtyNiceList(children.Select(c => new ListEntry(c.Child, c.Age, c.ToyId, naughty.Contains(c.Child))));
    }
}
