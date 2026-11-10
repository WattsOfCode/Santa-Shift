using System.Text.Json.Serialization;

namespace SantaShift.Core.Models;

/// <summary>A toy the workshop can build. Loaded from toys.json.</summary>
public sealed class Toy
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>
    /// Every piece that can go on this toy at the bench, each on its own part key. The plain toy uses some of them;
    /// a variant can swap one for a different piece (big wheels) or add one (a rocket pack).
    /// </summary>
    public List<ToyPart> Parts { get; init; } = [];

    /// <summary>
    /// The toy's build combinations: the plain toy first, then its variants. Each lists the parts in the order they
    /// must go on. Left out, the toy has one combination: every part, in the order <see cref="Parts"/> lists them.
    /// </summary>
    public List<ToyVariant> Variants { get; init; } = [];

    /// <summary>Model file for the finished toy, e.g. "toy_train.glb". A missing file falls back to grey-box shapes.</summary>
    public string? Model { get; init; }

    /// <summary>Model for a toy built in the wrong order (or too slowly). It can only be scrapped.</summary>
    public string? BotchedModel { get; init; }

    /// <summary>Hex colour used by grey-box shapes and UI icons, e.g. "#B94146".</summary>
    public string Color { get; init; } = "#8C6043";

    /// <summary>Letters ask for this toy from this round of a run (0 = from the start). Its bin gives no kits before.</summary>
    public int FromRound { get; init; }

    /// <summary>Whether this toy is made in round <paramref name="round"/>.</summary>
    public bool InRound(int round) => round >= FromRound;

    private List<ToyVariant>? _recipes;

    /// <summary>The build combinations, the plain toy first (see <see cref="Variants"/>).</summary>
    [JsonIgnore]
    public IReadOnlyList<ToyVariant> Recipes => _recipes ??= Variants.Count > 0
        ? Variants
        : [new ToyVariant { Id = ToyVariant.StandardId, Name = Name, Order = Parts.Select(p => p.Id).ToList() }];

    /// <summary>The plain toy's combination.</summary>
    [JsonIgnore]
    public ToyVariant Standard => Recipes[0];

    /// <summary>
    /// A combination by its stored id: null is the plain toy (orders and items keep null for it), anything else a
    /// variant. Null if there is no such variant.
    /// </summary>
    public ToyVariant? Variant(string? id) => id == null ? Standard : Recipes.Skip(1).FirstOrDefault(r => r.Id == id);

    /// <summary>The id an order or item keeps for a combination: null for the plain toy.</summary>
    public string? StoredId(ToyVariant recipe) => recipe == Standard ? null : recipe.Id;

    /// <summary>The name to show for a variant ("Big-Wheel Train"), or the toy's own name for the plain toy.</summary>
    public string NameOf(string? variantId) => Variant(variantId)?.Name is { Length: > 0 } name ? name : Name;

    /// <summary>The part key (1-9) that adds a part: its own "key", else its place in <see cref="Parts"/>. 0 if unknown.</summary>
    public int KeyOf(string partId)
    {
        int index = Parts.FindIndex(p => p.Id == partId);
        return index < 0 ? 0 : Parts[index].Key > 0 ? Parts[index].Key : index + 1;
    }

    public ToyPart? Part(string partId) => Parts.FirstOrDefault(p => p.Id == partId);
}

/// <summary>One piece of a toy, e.g. the train's wheels.</summary>
public sealed class ToyPart
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The part key that adds this piece (1 = key 1 / X). 0 = its place in the toy's part list.</summary>
    public int Key { get; init; }

    /// <summary>Model of this piece alone, placed where it sits on the finished toy (so pieces stack into the toy).</summary>
    public string? Model { get; init; }

    /// <summary>Raises the toy's other pieces by this many metres once it is on (big wheels lift the boiler and cab).</summary>
    public double Lift { get; init; }

    /// <summary>Scales the whole toy by this once it is on (extra stuffing makes a bigger bear). 0 or 1 = no change.</summary>
    public double Grow { get; init; }

    /// <summary>Goes inside the toy, so it isn't drawn once it is on (the stuffing). It still lies on the bench before.</summary>
    public bool Inside { get; init; }

    /// <summary>
    /// Model for a second (or later) copy of this part in a combination that uses it twice, e.g. the train's second set
    /// of wheels, which comes with the carriage coupled behind the cab. Null = the same model again.
    /// </summary>
    public string? RepeatModel { get; init; }
}

/// <summary>A build combination: the plain toy or one of its variants (big-wheel train, big bear, rocket robot).</summary>
public sealed class ToyVariant
{
    /// <summary>The id given to the plain toy when toys.json lists no variants.</summary>
    public const string StandardId = "standard";

    public string Id { get; init; } = "";

    /// <summary>Shown on order cards and the bench, e.g. "Big-Wheel Train". Empty = the toy's name.</summary>
    public string Name { get; init; } = "";

    /// <summary>A short tag for small order cards, e.g. "BIG". Empty for the plain toy.</summary>
    public string Tag { get; init; } = "";

    /// <summary>The part ids in the order they must go on.</summary>
    public List<string> Order { get; init; } = [];

    /// <summary>Letters can ask for this variant from this round on (0 = from the round variants start, see tuning.json).</summary>
    public int FromRound { get; init; }
}
