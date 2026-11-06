using SantaShift.Game.Services;

namespace SantaShift.Game.App;

/// <summary>Holds the active screen and switches cleanly between Title, Workshop and Results.</summary>
public sealed class SceneManager(IInputService input)
{
    private IScreen? _next;

    public IScreen? Current { get; private set; }
    public bool QuitRequested { get; private set; }

    /// <summary>Queues a switch. It happens between frames, so a screen never changes while it is running.</summary>
    public void Switch(IScreen next) => _next = next;

    public void RequestQuit() => QuitRequested = true;

    /// <summary>Called by the game loop at the start of each frame.</summary>
    public void ApplyPendingSwitch()
    {
        if (_next == null) return;
        Current?.Exit();
        Current = _next;
        _next = null;
        input.Clear(); // the press that changed screens must not act on the new one
        Current.Enter();
    }
}
