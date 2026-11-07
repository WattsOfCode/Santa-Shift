using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Persistence;
using SantaShift.Game.Services;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.App;

/// <summary>The shared parts every controller needs. Created once by <see cref="GameApp"/>.</summary>
public sealed class GameServices
{
    public required GameContent Content { get; init; }
    public required RenderSettings Render { get; init; }
    public required IInputService Input { get; init; }
    public required IAudioService Audio { get; init; }
    public required IAssetLibrary Assets { get; init; }
    public required SaveData Save { get; init; }
    public required SceneManager Scenes { get; init; }
    public required WorldSpace World { get; init; }

    public required Settings Settings { get; init; }
    public required PersonalBest Best { get; init; }

    /// <summary>Lifetime totals and unlocks, kept between runs.</summary>
    public required PlayerProgress Progress { get; init; }

    public void SaveProgress() => Save.SaveProgress(Progress);

    /// <summary>The hat colour the player chose from their unlocks, or null for the classic red.</summary>
    public Raylib_cs.Color? HatColour =>
        Content.Unlocks.FirstOrDefault(u => u.Id == Progress.Hat && u.Reward == UnlockReward.Hat) is { } hat
            ? Views.Palette.Hex(hat.Value) : null;

    /// <summary>False only for automated debug captures, which run without window focus.</summary>
    public bool PauseOnFocusLoss { get; init; } = true;

    public void SaveSettings() => Save.SaveSettings(Settings);
}
