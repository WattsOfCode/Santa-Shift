namespace SantaShift.Core.Models;

/// <summary>A Santa line paired with its plain mechanical text.</summary>
public sealed class DialogueCue
{
    public string Santa { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>All player-facing text. Loaded from dialogue.json, so wording changes never touch rules.</summary>
public sealed class DialogueBook
{
    public Dictionary<string, DialogueCue> Cues { get; init; } = new();
    public Dictionary<string, string> Messages { get; init; } = new();

    public DialogueCue? Cue(string? id) => id != null && Cues.TryGetValue(id, out var cue) ? cue : null;

    /// <summary>Returns the message with {0}, {1}... filled in. Unknown keys return the key itself, so gaps stay visible.</summary>
    public string Message(string key, params object[] args)
    {
        string template = Messages.TryGetValue(key, out var t) ? t : key;
        return args.Length == 0 ? template : string.Format(template, args);
    }
}
