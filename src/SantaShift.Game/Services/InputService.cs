using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Models;

namespace SantaShift.Game.Services;

public enum InputAction
{
    Interact, Confirm, Back, Pause, ToggleMode, Restart, Quit,

    /// <summary>Menu navigation: arrows, the bound move keys, the D-pad and flicks of the left stick.</summary>
    MenuUp, MenuDown, MenuLeft, MenuRight,

    /// <summary>Switch settings tabs: Tab / LB / RB.</summary>
    TabPrev, TabNext,

    /// <summary>Settings: clear the selected binding (Backspace / Delete, controller X).</summary>
    ClearBinding,

    /// <summary>Left mouse button (clicking a ribbon spool, a perk card).</summary>
    Click,

    /// <summary>Pick a part in the bench minigame. Part1 + (n - 1) is part key n (1 to 4 are rebindable).</summary>
    Part1, Part2, Part3, Part4, Part5, Part6, Part7, Part8, Part9,
}

/// <summary>
/// Reads player input from the keyboard, mouse and one controller. Swappable.
/// Presses are latched until a step consumes them, so a quick tap is never lost and a held key never repeats
/// (GDD section 7); a press nobody reads within a quarter of a second is dropped, so it can't fire later somewhere else.
/// </summary>
public interface IInputService
{
    /// <summary>Call once per rendered frame.</summary>
    void Poll();

    /// <summary>Movement direction, length 0..1, same speed diagonally (keys, D-pad or left stick).</summary>
    Vector2 Move();

    /// <summary>True once per press.</summary>
    bool ConsumePressed(InputAction action);

    /// <summary>The part key (1-9) pressed since the last call, or 0. Earlier presses come out first.</summary>
    int ConsumePartKey()
    {
        for (int n = 1; n <= 9; n++)
            if (ConsumePressed(InputAction.Part1 + (n - 1))) return n;
        return 0;
    }

    /// <summary>Forgets pending presses. Called on every screen change so one press can't act twice.</summary>
    void Clear();

    /// <summary>
    /// How far to turn the camera this frame, in mouse pixels: the mouse plus the right stick. Zero while the mouse is
    /// not captured (menus).
    /// </summary>
    Vector2 Look();

    /// <summary>Hides and locks the cursor for mouse look (true), or frees it for menus (false).</summary>
    void SetMouseCaptured(bool captured);

    /// <summary>Where the mouse pointer is, in window pixels.</summary>
    Vector2 Pointer() => Raylib.GetMousePosition();

    /// <summary>True when the controller was used last (prompts then show its buttons).</summary>
    bool UsingGamepad { get; }

    /// <summary>What to press for an action on the device used last, for prompts ("E", "A", "1"...).</summary>
    string Label(InputAction action);

    /// <summary>Uses these bindings from now on (after a change in Settings > Controls).</summary>
    void Apply(ControlBindings bindings);

    /// <summary>For rebinding: the keyboard key pressed this frame (Raylib name), if any. Esc, Enter are never offered.</summary>
    bool TryCaptureKey(out string key);

    /// <summary>For rebinding: the controller button pressed this frame (Raylib name), if any. Start is never offered.</summary>
    bool TryCaptureButton(out string button);
}

public sealed class PlayerInputService : IInputService
{
    /// <summary>Right stick at full tilt turns the camera like this many mouse pixels per second.</summary>
    private const float StickLookPixelsPerSecond = 1200f;
    private const float StickDeadZone = 0.2f;
    /// <summary>The camera-turn keys turn it like this many mouse pixels per second (about 120 degrees a second).</summary>
    private const float KeyLookPixelsPerSecond = 800f;
    private const double LatchSeconds = 0.25;

    // Fixed controls that always work, whatever the bindings say.
    private static readonly Dictionary<InputAction, KeyboardKey[]> FixedKeys = new()
    {
        [InputAction.Confirm] = [KeyboardKey.Enter, KeyboardKey.KpEnter, KeyboardKey.Space],
        [InputAction.Back] = [KeyboardKey.Escape],
        [InputAction.Pause] = [KeyboardKey.Escape, KeyboardKey.P],
        [InputAction.ToggleMode] = [KeyboardKey.Tab],
        [InputAction.Restart] = [KeyboardKey.R],
        [InputAction.Quit] = [KeyboardKey.Q],
        [InputAction.MenuUp] = [KeyboardKey.Up],
        [InputAction.MenuDown] = [KeyboardKey.Down],
        [InputAction.MenuLeft] = [KeyboardKey.Left],
        [InputAction.MenuRight] = [KeyboardKey.Right],
        [InputAction.TabNext] = [KeyboardKey.Tab, KeyboardKey.PageDown],
        [InputAction.TabPrev] = [KeyboardKey.PageUp],
        [InputAction.ClearBinding] = [KeyboardKey.Backspace, KeyboardKey.Delete],
        [InputAction.Part5] = [KeyboardKey.Five, KeyboardKey.Kp5],
        [InputAction.Part6] = [KeyboardKey.Six, KeyboardKey.Kp6],
        [InputAction.Part7] = [KeyboardKey.Seven, KeyboardKey.Kp7],
        [InputAction.Part8] = [KeyboardKey.Eight, KeyboardKey.Kp8],
        [InputAction.Part9] = [KeyboardKey.Nine, KeyboardKey.Kp9],
    };

    private static readonly Dictionary<InputAction, GamepadButton[]> FixedButtons = new()
    {
        [InputAction.Confirm] = [GamepadButton.RightFaceDown],
        [InputAction.Back] = [GamepadButton.RightFaceRight],
        [InputAction.Pause] = [GamepadButton.MiddleRight],
        [InputAction.MenuUp] = [GamepadButton.LeftFaceUp],
        [InputAction.MenuDown] = [GamepadButton.LeftFaceDown],
        [InputAction.MenuLeft] = [GamepadButton.LeftFaceLeft],
        [InputAction.MenuRight] = [GamepadButton.LeftFaceRight],
        [InputAction.TabPrev] = [GamepadButton.LeftTrigger1],
        [InputAction.TabNext] = [GamepadButton.RightTrigger1],
        [InputAction.ClearBinding] = [GamepadButton.RightFaceLeft],
        [InputAction.ToggleMode] = [GamepadButton.RightFaceUp],
    };

    /// <summary>Which rebindable action feeds which input actions (the move keys also steer menus).</summary>
    private static readonly (string Bind, InputAction[] Actions)[] BoundActions =
    [
        (ControlBindings.MoveUp, [InputAction.MenuUp]),
        (ControlBindings.MoveDown, [InputAction.MenuDown]),
        (ControlBindings.MoveLeft, [InputAction.MenuLeft]),
        (ControlBindings.MoveRight, [InputAction.MenuRight]),
        (ControlBindings.Interact, [InputAction.Interact, InputAction.Confirm]),
        (ControlBindings.Part1, [InputAction.Part1]),
        (ControlBindings.Part2, [InputAction.Part2]),
        (ControlBindings.Part3, [InputAction.Part3]),
        (ControlBindings.Part4, [InputAction.Part4]),
        (ControlBindings.CameraLeft, []), // held, read by Look()
        (ControlBindings.CameraRight, []),
    ];

    private readonly Dictionary<string, KeyboardKey[]> _keys = new();
    private readonly Dictionary<string, GamepadButton?> _buttons = new();
    private ControlBindings _bindings = ControlBindings.Defaults();

    private readonly Dictionary<InputAction, double> _latched = new();
    private int _pad = -1;
    private float _lastStickY, _lastStickX;
    private int _capturedKey, _capturedButton;
    private double _capturedKeyAt, _capturedButtonAt;

    public bool UsingGamepad { get; private set; }

    public PlayerInputService(ControlBindings bindings) => Apply(bindings);

    public void Apply(ControlBindings bindings)
    {
        _bindings = bindings;
        _keys.Clear();
        _buttons.Clear();
        foreach (var action in ControlBindings.Actions)
        {
            _keys[action] = Enumerable.Range(0, ControlBindings.KeySlots)
                .Select(i => Enum.TryParse<KeyboardKey>(bindings.KeyAt(action, i), out var k) ? k : KeyboardKey.Null)
                .Where(k => k != KeyboardKey.Null).ToArray();
            _buttons[action] = Enum.TryParse<GamepadButton>(bindings.PadFor(action), out var b) ? b : null;
        }
    }

    public void Poll()
    {
        double now = Raylib.GetTime();
        void Latch(InputAction a) => _latched[a] = now;

        foreach (var (action, keys) in FixedKeys)
            foreach (var key in keys)
                if (Raylib.IsKeyPressed(key)) { Latch(action); UsingGamepad = false; }

        foreach (var (bind, actions) in BoundActions)
            foreach (var key in _keys[bind])
                if (Raylib.IsKeyPressed(key)) { foreach (var a in actions) Latch(a); UsingGamepad = false; }

        if (Raylib.GetMouseDelta().LengthSquared() > 4) UsingGamepad = false;
        if (Raylib.IsMouseButtonPressed(MouseButton.Left)) { Latch(InputAction.Click); UsingGamepad = false; }
        if (Raylib.IsMouseButtonPressed(MouseButton.Right)) { Latch(InputAction.Back); UsingGamepad = false; } // right click backs out of menus

        // The first connected controller.
        _pad = -1;
        for (int i = 0; i < 4 && _pad < 0; i++) if (Raylib.IsGamepadAvailable(i)) _pad = i;
        if (_pad >= 0)
        {
            foreach (var (action, buttons) in FixedButtons)
                foreach (var b in buttons)
                    if (Raylib.IsGamepadButtonPressed(_pad, b)) { Latch(action); UsingGamepad = true; }
            foreach (var (bind, actions) in BoundActions)
                if (_buttons[bind] is { } b && Raylib.IsGamepadButtonPressed(_pad, b)) { foreach (var a in actions) Latch(a); UsingGamepad = true; }

            int pressed = Raylib.GetGamepadButtonPressed();
            if (pressed > 0 && Raylib.IsGamepadButtonPressed(_pad, (GamepadButton)pressed)) (_capturedButton, _capturedButtonAt) = (pressed, now);

            // Flicking the left stick steps through menus like the D-pad.
            var stick = LeftStick();
            if (stick.Y < -0.5f && _lastStickY >= -0.5f) Latch(InputAction.MenuUp);
            if (stick.Y > 0.5f && _lastStickY <= 0.5f) Latch(InputAction.MenuDown);
            if (stick.X < -0.5f && _lastStickX >= -0.5f) Latch(InputAction.MenuLeft);
            if (stick.X > 0.5f && _lastStickX <= 0.5f) Latch(InputAction.MenuRight);
            (_lastStickX, _lastStickY) = (stick.X, stick.Y);
            if (stick.LengthSquared() > 0.1f || RightStick().LengthSquared() > 0.1f) UsingGamepad = true;
        }

        bool first = true;
        for (int key = Raylib.GetKeyPressed(); key != 0; key = Raylib.GetKeyPressed())
            if (first) { (_capturedKey, _capturedKeyAt) = (key, now); first = false; } // first key this frame; the rest is dropped
    }

    private bool Held(string bind) =>
        _keys[bind].Any(k => Raylib.IsKeyDown(k)) || (_pad >= 0 && _buttons[bind] is { } b && Raylib.IsGamepadButtonDown(_pad, b));

    public Vector2 Move()
    {
        float x = 0, y = 0;
        bool Down(string bind) =>
            _keys[bind].Any(k => Raylib.IsKeyDown(k)) ||
            (_pad >= 0 && _buttons[bind] is { } b && Raylib.IsGamepadButtonDown(_pad, b));
        if (Down(ControlBindings.MoveLeft)) x -= 1;
        if (Down(ControlBindings.MoveRight)) x += 1;
        if (Down(ControlBindings.MoveUp)) y -= 1;
        if (Down(ControlBindings.MoveDown)) y += 1;
        var v = new Vector2(x, y);
        if (v.LengthSquared() > 1) v = Vector2.Normalize(v);
        var stick = LeftStick();
        return stick.LengthSquared() > v.LengthSquared() ? stick : v;
    }

    public bool ConsumePressed(InputAction action)
    {
        if (!_latched.Remove(action, out double at)) return false;
        return Raylib.GetTime() - at <= LatchSeconds;
    }

    public void Clear() => _latched.Clear();

    private bool _captured;
    private bool _skipNextDelta;
    private Settings? _settings;

    /// <summary>Gives look sensitivity from the player's settings (read live, so changes apply at once).</summary>
    public void UseSettings(Settings settings) => _settings = settings;

    public Vector2 Look()
    {
        if (!_captured) return Vector2.Zero;
        var look = Vector2.Zero;
        if (_skipNextDelta) _skipNextDelta = false; // the cursor jump when locking is not a turn
        else look = Raylib.GetMouseDelta() * (_settings?.MouseSensitivity ?? 1f);
        float dt = Raylib.GetFrameTime();
        look += RightStick() * StickLookPixelsPerSecond * dt * (_settings?.StickSensitivity ?? 1f);
        // Turning the camera with keys or the triggers: for trackpads, or anyone who prefers it.
        float turn = (Held(ControlBindings.CameraRight) ? 1 : 0) - (Held(ControlBindings.CameraLeft) ? 1 : 0);
        look.X += turn * KeyLookPixelsPerSecond * dt * (_settings?.StickSensitivity ?? 1f);
        return look;
    }

    public void SetMouseCaptured(bool captured)
    {
        if (captured == _captured) return;
        _captured = captured;
        if (captured) { Raylib.DisableCursor(); _skipNextDelta = true; }
        else Raylib.EnableCursor();
    }

    public string Label(InputAction action)
    {
        string? bind = BoundActions.FirstOrDefault(b => b.Actions.Contains(action)).Bind;
        if (action == InputAction.Confirm) bind = null; // menus show Enter / A
        if (UsingGamepad)
        {
            if (bind != null) return InputNames.Button(_bindings.PadFor(bind));
            return FixedButtons.TryGetValue(action, out var bs) ? InputNames.Button(bs[0].ToString()) : "";
        }
        if (bind != null)
        {
            string first = Enumerable.Range(0, ControlBindings.KeySlots).Select(i => _bindings.KeyAt(bind, i)).FirstOrDefault(k => k != "") ?? "";
            return InputNames.Key(first);
        }
        return FixedKeys.TryGetValue(action, out var ks) ? InputNames.Key(ks[0].ToString()) : "";
    }

    private static readonly HashSet<KeyboardKey> Reserved = [KeyboardKey.Escape, KeyboardKey.Enter, KeyboardKey.KpEnter];

    public bool TryCaptureKey(out string key)
    {
        key = "";
        if (_capturedKey == 0 || Raylib.GetTime() - _capturedKeyAt > LatchSeconds || Reserved.Contains((KeyboardKey)_capturedKey)) return false;
        key = ((KeyboardKey)_capturedKey).ToString();
        _capturedKey = 0;
        return true;
    }

    public bool TryCaptureButton(out string button)
    {
        button = "";
        if (_capturedButton == 0 || Raylib.GetTime() - _capturedButtonAt > LatchSeconds
            || (GamepadButton)_capturedButton == GamepadButton.MiddleRight) return false;
        button = ((GamepadButton)_capturedButton).ToString();
        _capturedButton = 0;
        return true;
    }

    private Vector2 LeftStick() => Stick(GamepadAxis.LeftX, GamepadAxis.LeftY);
    private Vector2 RightStick() => Stick(GamepadAxis.RightX, GamepadAxis.RightY);

    /// <summary>A stick with its dead zone removed and rescaled, so it starts from zero just outside it.</summary>
    private Vector2 Stick(GamepadAxis ax, GamepadAxis ay)
    {
        if (_pad < 0) return Vector2.Zero;
        var v = new Vector2(Raylib.GetGamepadAxisMovement(_pad, ax), Raylib.GetGamepadAxisMovement(_pad, ay));
        float len = v.Length();
        if (len < StickDeadZone) return Vector2.Zero;
        return v / len * Math.Min(1f, (len - StickDeadZone) / (1 - StickDeadZone));
    }
}

/// <summary>Readable names for Raylib keys and controller buttons (Xbox layout), for prompts and the settings screen.</summary>
public static class InputNames
{
    public static string Key(string name) => name switch
    {
        "" => "-",
        "One" => "1", "Two" => "2", "Three" => "3", "Four" => "4", "Five" => "5",
        "Six" => "6", "Seven" => "7", "Eight" => "8", "Nine" => "9", "Zero" => "0",
        "LeftShift" => "L-Shift", "RightShift" => "R-Shift", "LeftControl" => "L-Ctrl", "RightControl" => "R-Ctrl",
        "LeftAlt" => "L-Alt", "RightAlt" => "R-Alt", "Escape" => "Esc", "Enter" => "Enter", "Backspace" => "Bksp",
        "PageUp" => "PgUp", "PageDown" => "PgDn", "CapsLock" => "Caps",
        _ when name.StartsWith("Kp") => "Num " + name[2..],
        _ => name,
    };

    public static string Button(string name) => name switch
    {
        "" => "-",
        "RightFaceDown" => "A", "RightFaceRight" => "B", "RightFaceLeft" => "X", "RightFaceUp" => "Y",
        "LeftFaceUp" => "D-Up", "LeftFaceDown" => "D-Down", "LeftFaceLeft" => "D-Left", "LeftFaceRight" => "D-Right",
        "LeftTrigger1" => "LB", "LeftTrigger2" => "LT", "RightTrigger1" => "RB", "RightTrigger2" => "RT",
        "MiddleLeft" => "View", "MiddleRight" => "Start", "Middle" => "Guide",
        "LeftThumb" => "L-Stick", "RightThumb" => "R-Stick",
        _ => name,
    };
}
