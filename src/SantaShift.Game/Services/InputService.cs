using System.Numerics;
using Raylib_cs;

namespace SantaShift.Game.Services;

public enum InputAction { Interact, Confirm, Back, ToggleMode, Restart, Quit }

/// <summary>
/// Reads player input. Swappable: a gamepad or remappable version only needs to implement this.
/// Presses are latched until a simulation step consumes them, so a quick tap is never lost
/// and a held key never repeats (GDD section 7).
/// </summary>
public interface IInputService
{
    /// <summary>Call once per rendered frame.</summary>
    void Poll();

    /// <summary>Movement direction, length 0..1, same speed diagonally.</summary>
    Vector2 Move();

    /// <summary>True once per press.</summary>
    bool ConsumePressed(InputAction action);

    /// <summary>Forgets pending presses. Called on every screen change so one press can't act twice.</summary>
    void Clear();

    /// <summary>Mouse movement since the last frame, in pixels. Zero while the mouse is not captured.</summary>
    Vector2 MouseDelta();

    /// <summary>Hides and locks the cursor for mouse look (true), or frees it for menus (false).</summary>
    void SetMouseCaptured(bool captured);
}

public sealed class KeyboardInputService : IInputService
{
    private static readonly Dictionary<InputAction, KeyboardKey[]> Bindings = new()
    {
        [InputAction.Interact] = [KeyboardKey.E, KeyboardKey.Space],
        [InputAction.Confirm] = [KeyboardKey.Enter, KeyboardKey.KpEnter, KeyboardKey.Space, KeyboardKey.E],
        [InputAction.Back] = [KeyboardKey.Escape],
        [InputAction.ToggleMode] = [KeyboardKey.Tab, KeyboardKey.M],
        [InputAction.Restart] = [KeyboardKey.R],
        [InputAction.Quit] = [KeyboardKey.Q],
    };

    private readonly HashSet<InputAction> _latched = new();

    public void Poll()
    {
        foreach (var (action, keys) in Bindings)
            foreach (var key in keys)
                if (Raylib.IsKeyPressed(key)) _latched.Add(action);
    }

    public Vector2 Move()
    {
        float x = 0, y = 0;
        if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) x -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) x += 1;
        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) y -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) y += 1;
        var v = new Vector2(x, y);
        return v.LengthSquared() > 1 ? Vector2.Normalize(v) : v;
    }

    public bool ConsumePressed(InputAction action) => _latched.Remove(action);

    public void Clear() => _latched.Clear();

    private bool _captured;
    private bool _skipNextDelta;

    public Vector2 MouseDelta()
    {
        if (!_captured) return Vector2.Zero;
        if (_skipNextDelta) { _skipNextDelta = false; return Vector2.Zero; } // the cursor jump when locking is not a turn
        return Raylib.GetMouseDelta();
    }

    public void SetMouseCaptured(bool captured)
    {
        if (captured == _captured) return;
        _captured = captured;
        if (captured) { Raylib.DisableCursor(); _skipNextDelta = true; }
        else Raylib.EnableCursor();
    }
}
