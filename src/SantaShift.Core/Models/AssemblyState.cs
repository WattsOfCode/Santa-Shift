namespace SantaShift.Core.Models;

/// <summary>
/// The bench minigame in progress: put the toy's parts on with its build combination, before time runs out.
/// Each part sits on its own part key (the same key every time for that toy). The parts pressed so far decide which
/// combination is being built (the plain toy or a variant); a part that fits none of them, or running out of time,
/// botches the toy.
/// </summary>
public sealed class AssemblyState
{
    public required string StationId { get; init; }
    public required string ToyId { get; init; }
    public int ItemRevision { get; set; }

    /// <summary>Part ids by key: Keys[0] is on key 1, Keys[1] on key 2, and so on ("" = no part on that key).</summary>
    public required IReadOnlyList<string> Keys { get; init; }

    /// <summary>The combinations that can be built here this round, the plain toy first.</summary>
    public required IReadOnlyList<ToyVariant> Recipes { get; init; }

    /// <summary>
    /// The variant the most urgent letter for this toy wants (null = the plain toy). Only a hint: the bench lists it
    /// first, and <see cref="NextPart"/> follows it while the parts placed still fit it.
    /// </summary>
    public string? Wanted { get; init; }

    /// <summary>True when a nice child's letter for this toy is waiting (so <see cref="Wanted"/> comes from a letter).</summary>
    public bool LetterWaiting { get; init; }

    /// <summary>The parts added so far, in order.</summary>
    public List<string> PlacedParts { get; } = [];

    /// <summary>How many parts are on.</summary>
    public int Placed => PlacedParts.Count;

    public long StartTick { get; init; }
    public long DeadlineTick { get; init; }

    /// <summary>The id an item or order keeps for one of <see cref="Recipes"/>: null for the plain toy (the first).</summary>
    public string? StoredId(ToyVariant recipe) => Recipes.Count > 0 && recipe == Recipes[0] ? null : recipe.Id;

    /// <summary>True while the parts placed so far are how <paramref name="recipe"/> starts and it still needs more.</summary>
    public bool Fits(ToyVariant recipe) =>
        recipe.Order.Count > Placed && recipe.Order.Take(Placed).SequenceEqual(PlacedParts);

    /// <summary>The combination being followed: the wanted one while it still fits, else the first that does.</summary>
    public ToyVariant? Target
    {
        get
        {
            ToyVariant? first = null;
            foreach (var recipe in Recipes)
            {
                if (!Fits(recipe)) continue;
                if (StoredId(recipe) == Wanted) return recipe;
                first ??= recipe;
            }
            return first;
        }
    }

    public string? NextPart => Target is { } r ? r.Order[Placed] : null;

    /// <summary>The key (1-based) that adds the next part of <see cref="Target"/>, or 0 when there is none.</summary>
    public int NextKey => NextPart is { } id ? KeyOf(id) : 0;

    /// <summary>The key (1-based) a part is on, or 0.</summary>
    public int KeyOf(string partId)
    {
        for (int i = 0; i < Keys.Count; i++)
            if (Keys[i] == partId) return i + 1;
        return 0;
    }

    /// <summary>1 at the start, 0 when time is up.</summary>
    public double TimeLeft(long tick)
    {
        long total = DeadlineTick - StartTick;
        return total <= 0 ? 0 : Math.Clamp((double)(DeadlineTick - tick) / total, 0, 1);
    }
}
