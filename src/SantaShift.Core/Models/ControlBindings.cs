namespace SantaShift.Core.Models;

/// <summary>
/// The player's controls: up to two keyboard keys and one controller button for each action, saved with the settings.
/// Names are plain text (Raylib's key and gamepad button names, e.g. "E", "Space", "RightFaceDown"), so the rules
/// project never depends on Raylib. Pause / back (Esc, Start), confirm (Enter) and the menu arrows are fixed and
/// always work, so a bad binding can never lock the player out.
/// </summary>
public sealed class ControlBindings
{
    public const int KeySlots = 2;

    public const string MoveUp = "MoveUp", MoveDown = "MoveDown", MoveLeft = "MoveLeft", MoveRight = "MoveRight";
    public const string Interact = "Interact", Part1 = "Part1", Part2 = "Part2", Part3 = "Part3";
    public const string CameraLeft = "CameraLeft", CameraRight = "CameraRight";

    /// <summary>Every action the player can rebind, in the order the settings screen lists them.</summary>
    public static readonly string[] Actions = [MoveUp, MoveDown, MoveLeft, MoveRight, Interact, Part1, Part2, Part3, CameraLeft, CameraRight];

    /// <summary>Keyboard keys by action; each array has <see cref="KeySlots"/> entries ("" = empty slot).</summary>
    public Dictionary<string, string[]> Keys { get; set; } = new();

    /// <summary>Controller button by action ("" = none).</summary>
    public Dictionary<string, string> Pad { get; set; } = new();

    public static ControlBindings Defaults() => new()
    {
        Keys = new()
        {
            [MoveUp] = ["W", "Up"], [MoveDown] = ["S", "Down"], [MoveLeft] = ["A", "Left"], [MoveRight] = ["D", "Right"],
            [Interact] = ["E", "Space"],
            [Part1] = ["One", "Kp1"], [Part2] = ["Two", "Kp2"], [Part3] = ["Three", "Kp3"],
            [CameraLeft] = ["Z", "Comma"], [CameraRight] = ["C", "Period"],
        },
        Pad = new()
        {
            [MoveUp] = "LeftFaceUp", [MoveDown] = "LeftFaceDown", [MoveLeft] = "LeftFaceLeft", [MoveRight] = "LeftFaceRight",
            [Interact] = "RightFaceDown",
            [Part1] = "RightFaceLeft", [Part2] = "RightFaceUp", [Part3] = "RightTrigger1",
            [CameraLeft] = "LeftTrigger2", [CameraRight] = "RightTrigger2",
        },
    };

    public string KeyAt(string action, int slot) =>
        Keys.TryGetValue(action, out var keys) && slot >= 0 && slot < keys.Length ? keys[slot] : "";

    public string PadFor(string action) => Pad.GetValueOrDefault(action, "");

    /// <summary>
    /// Puts <paramref name="key"/> in an action's slot. A key does one thing only: if another action (or the other slot)
    /// had it, that slot is emptied, so there are never two actions on one key.
    /// </summary>
    public void SetKey(string action, int slot, string key)
    {
        if (!Actions.Contains(action) || slot < 0 || slot >= KeySlots) return;
        if (key != "")
            foreach (var keys in Keys.Values)
                for (int i = 0; i < keys.Length; i++)
                    if (keys[i] == key) keys[i] = "";
        Keys[action][slot] = key;
    }

    /// <summary>Puts a controller button on an action, taking it off any other action.</summary>
    public void SetPad(string action, string button)
    {
        if (!Actions.Contains(action)) return;
        if (button != "")
            foreach (var other in Pad.Keys.ToList())
                if (Pad[other] == button) Pad[other] = "";
        Pad[action] = button;
    }

    /// <summary>
    /// Fixes a loaded or hand-edited set: every action present with exactly <see cref="KeySlots"/> keys, missing ones
    /// back to their defaults, unknown actions dropped, and a key or button used twice kept only on its first action.
    /// </summary>
    public ControlBindings Normalized()
    {
        var d = Defaults();
        var keys = new Dictionary<string, string[]>();
        var pad = new Dictionary<string, string>();
        var usedKeys = new HashSet<string>();
        var usedPad = new HashSet<string>();
        foreach (var action in Actions)
        {
            var source = Keys?.GetValueOrDefault(action) ?? d.Keys[action];
            var slots = new string[KeySlots];
            for (int i = 0; i < KeySlots; i++)
            {
                string k = i < source.Length ? source[i] ?? "" : "";
                slots[i] = k != "" && usedKeys.Add(k) ? k : "";
            }
            keys[action] = slots;
            string b = Pad?.GetValueOrDefault(action) ?? d.Pad[action];
            pad[action] = b != "" && usedPad.Add(b) ? b : "";
        }
        return new ControlBindings { Keys = keys, Pad = pad };
    }
}
