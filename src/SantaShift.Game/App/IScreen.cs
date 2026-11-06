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

    /// <summary>Draws the 3D scene into the internal render texture.</summary>
    void DrawWorld();

    /// <summary>Draws 2D interface on top, in logical 960 x 540 coordinates.</summary>
    void DrawUi(UiCanvas ui);

    void Exit() { }
}
