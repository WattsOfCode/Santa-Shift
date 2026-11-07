using System.Numerics;
using Raylib_cs;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.World;

/// <summary>
/// One elf on screen (the player or a workshop elf): poses the shared elf model with its own animator, draws it,
/// and draws the tool in its right hand (hammer while hammering, shovel while shovelling).
/// </summary>
public sealed class ElfFigure(IAssetLibrary assets)
{
    private ElfAnimator? _animator;
    private int _hatMaterial = -2; // -2: not looked for yet, -1: none
    private Color _hatDefault;

    /// <summary>The hat colour (an unlock), or null for the model's own red.</summary>
    public Color? Hat { get; set; }

    /// <summary>True once the elf model has animations (then the caller's bob isn't needed).</summary>
    public bool IsAnimated => _animator?.IsAnimated ?? false;

    /// <summary>True once the elf model has a Carry pose and a right hand to hold things in.</summary>
    public bool CanCarry => _animator?.CanCarry ?? false;

    /// <summary>Draws the elf standing at <paramref name="at"/>. Returns false if there is no elf model.</summary>
    public bool Draw(Vector3 at, float facingDegrees, ElfPose pose, float dt)
    {
        if (!assets.TryGetModel(AssetLibrary.ElfModel, out var model)) return false;
        _animator ??= new ElfAnimator(model, assets.Animations(AssetLibrary.ElfModel));
        using (App.FrameProfiler.Time("elf skinning")) _animator.Update(model, dt, pose);
        SetHat(model, Hat);
        using (App.FrameProfiler.Time("elf draw"))
            Raylib.DrawModelEx(model, at, Vector3.UnitY, facingDegrees, Vector3.One, Color.White);
        SetHat(model, null); // the model is shared: the workshop elves keep their red hats

        string? tool = pose.Activity switch
        {
            ElfActivity.Hammering => AssetLibrary.HammerModel,
            ElfActivity.Shovelling => AssetLibrary.ShovelModel,
            _ => null,
        };
        if (tool != null && assets.TryGetModel(tool, out var toolModel) && _animator.ToolTransform(model, pose.Activity, out var local))
        {
            var elfWorld = Raymath.MatrixMultiply(Raymath.MatrixRotateY(facingDegrees * MathF.PI / 180f),
                Raymath.MatrixTranslate(at.X, at.Y, at.Z));
            toolModel.Transform = Raymath.MatrixMultiply(local, elfWorld);
            Raylib.DrawModel(toolModel, Vector3.Zero, 1f, Color.White);
        }
        return true;
    }

    /// <summary>
    /// Recolours the hat: the elf material closest to the hat's berry red (found once). Raylib keeps no material names,
    /// so the colour is the way to find it.
    /// </summary>
    private unsafe void SetHat(Model model, Color? colour)
    {
        if (_hatMaterial == -2)
        {
            _hatMaterial = -1;
            int best = int.MaxValue;
            for (int i = 0; i < model.MaterialCount; i++)
            {
                var c = model.Materials[i].Maps[(int)MaterialMapIndex.Albedo].Color;
                int d = Math.Abs(c.R - 186) + Math.Abs(c.G - 45) + Math.Abs(c.B - 51);
                if (d < best && d < 40) (best, _hatMaterial, _hatDefault) = (d, i, c);
            }
        }
        if (_hatMaterial < 0) return;
        model.Materials[_hatMaterial].Maps[(int)MaterialMapIndex.Albedo].Color = colour ?? _hatDefault;
    }

    /// <summary>Where a carried item sits in the world (on the elf's raised hand). Call right after <see cref="Draw"/>.</summary>
    public bool TryHoldPoint(Vector3 at, float facingDegrees, out Vector3 world)
    {
        world = default;
        if (_animator == null || !assets.TryGetModel(AssetLibrary.ElfModel, out var model) || !_animator.CanCarry || !_animator.HoldPoint(model, out var local))
            return false;
        var elfWorld = Raymath.MatrixMultiply(Raymath.MatrixRotateY(facingDegrees * MathF.PI / 180f),
            Raymath.MatrixTranslate(at.X, at.Y, at.Z));
        world = Raymath.Vector3Transform(local, elfWorld);
        return true;
    }
}
