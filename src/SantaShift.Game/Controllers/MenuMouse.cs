using System.Numerics;
using Raylib_cs;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Controllers;

/// <summary>
/// The mouse in a menu: where the pointer is (logical UI coordinates), whether it moved since the last step, and
/// whether it clicked. Menus change their highlighted choice only when the mouse moves, so a resting pointer never
/// fights the keys or the controller. Left click selects; right click is Back (see InputService).
/// </summary>
public sealed class MenuMouse(IInputService input)
{
    private Vector2? _last;

    public Vector2 At { get; private set; }
    public bool Moved { get; private set; }
    public bool Clicked { get; private set; }

    /// <summary>True while the left button is held (dragging a slider).</summary>
    public static bool Held => Raylib.IsMouseButtonDown(MouseButton.Left);

    /// <summary>Call once per step before reading; it consumes this step's click.</summary>
    public void Update()
    {
        At = UiCanvas.FromScreen(input.Pointer(), Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        Moved = _last is { } last && Vector2.DistanceSquared(At, last) > 0.25f;
        _last = At;
        Clicked = input.ConsumePressed(InputAction.Click);
    }
}
