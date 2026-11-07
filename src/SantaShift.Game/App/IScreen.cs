using SantaShift.Game.Views.UI;

namespace SantaShift.Game.App;

/// <summary>
/// One screen of the game (title, workshop, results). Each screen is a controller that owns its views.
/// </summary>
public interface IScreen
{
    void Enter() { }

    /// <summary>Runs exactly 60 times per second of game time.</summary>
    void FixedUpdate();

    /// <summary>Runs once per rendered frame, after the fixed steps (smooth things like the camera).</summary>
    void FrameUpdate() { }

    /// <summary>Draws the 3D scene into the internal render texture.</summary>
    void DrawWorld();

    /// <summary>Draws 2D interface on top, in logical 960 x 540 coordinates.</summary>
    void DrawUi(UiCanvas ui);

    /// <summary>The music track for this screen (see <see cref="Services.MusicIds"/>); null = silence. Crossfades on change.</summary>
    string? Music => Services.MusicIds.Menu;

    void Exit() { }
}
