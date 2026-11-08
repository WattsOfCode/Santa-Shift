namespace SantaShift.Core.Models;

/// <summary>The credits shown in Settings > Credits, from credits.json: sections in two columns, then a footer.</summary>
public sealed class CreditsBook
{
    public List<CreditSection> Left { get; init; } = [];
    public List<CreditSection> Right { get; init; } = [];

    /// <summary>Lines across the bottom (copyright and licence).</summary>
    public List<string> Footer { get; init; } = [];

    public bool IsEmpty => Left.Count == 0 && Right.Count == 0 && Footer.Count == 0;
}

public sealed class CreditSection
{
    public string Heading { get; init; } = "";
    public List<string> Lines { get; init; } = [];
}
