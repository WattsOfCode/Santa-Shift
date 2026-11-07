namespace SantaShift.Core.Models;

/// <summary>What a perk changes for the rest of the run.</summary>
public enum PerkEffect
{
    /// <summary>Walk faster: +Amount as a fraction (0.12 = 12%).</summary>
    WalkSpeed,
    /// <summary>More time for the bench minigame: +Amount seconds.</summary>
    BenchTime,
    /// <summary>Tie ribbons faster: time x (1 - Amount).</summary>
    RibbonSpeed,
    /// <summary>Shovel coal faster: time x (1 - Amount).</summary>
    ShovelSpeed,
    /// <summary>Supply crates hold +Amount more kits.</summary>
    SupplyKits,
    /// <summary>Longer order deadlines: +Amount as a fraction.</summary>
    Deadlines,
    /// <summary>More points per delivery: +Amount as a fraction.</summary>
    ScoreBonus,
}

/// <summary>An upgrade Santa offers between rounds (perks.json). Picked ones last for the rest of the run.</summary>
public sealed class Perk
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public PerkEffect Effect { get; init; }
    public double Amount { get; init; }

    /// <summary>How many times it can be picked in one run.</summary>
    public int MaxStacks { get; init; } = 3;
}

/// <summary>The combined effect of every perk picked so far in a run. Rules read this; <see cref="None"/> changes nothing.</summary>
public sealed record RunModifiers
{
    public static readonly RunModifiers None = new();

    public double SpeedMultiplier { get; init; } = 1;
    public double BenchSeconds { get; init; }
    public double RibbonTimeMultiplier { get; init; } = 1;
    public double ShovelTimeMultiplier { get; init; } = 1;
    public int ExtraSupplyKits { get; init; }
    public double DeadlineMultiplier { get; init; } = 1;
    public double ScoreMultiplier { get; init; } = 1;

    public static RunModifiers From(IEnumerable<Perk> perks)
    {
        var m = new RunModifiers();
        foreach (var p in perks)
        {
            m = p.Effect switch
            {
                PerkEffect.WalkSpeed => m with { SpeedMultiplier = m.SpeedMultiplier + p.Amount },
                PerkEffect.BenchTime => m with { BenchSeconds = m.BenchSeconds + p.Amount },
                PerkEffect.RibbonSpeed => m with { RibbonTimeMultiplier = m.RibbonTimeMultiplier * Math.Clamp(1 - p.Amount, 0.1, 1) },
                PerkEffect.ShovelSpeed => m with { ShovelTimeMultiplier = m.ShovelTimeMultiplier * Math.Clamp(1 - p.Amount, 0.1, 1) },
                PerkEffect.SupplyKits => m with { ExtraSupplyKits = m.ExtraSupplyKits + (int)Math.Round(p.Amount) },
                PerkEffect.Deadlines => m with { DeadlineMultiplier = m.DeadlineMultiplier + p.Amount },
                PerkEffect.ScoreBonus => m with { ScoreMultiplier = m.ScoreMultiplier + p.Amount },
                _ => m,
            };
        }
        return m;
    }
}
