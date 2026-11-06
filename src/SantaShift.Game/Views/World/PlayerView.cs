using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.World;

/// <summary>Draws the elf (animated when elf.glb has actions), its hammer while building, its shadow, and the carried item.</summary>
public sealed class PlayerView(IAssetLibrary assets, WorldSpace world, ItemView items)
{
    public const float CarryHeight = 1.4f;

    /// <summary>Carried items are drawn smaller so they do not block the view ahead.</summary>
    public const float CarriedScale = 0.75f;

    private ElfAnimator? _animator;
    private float? _modelTop;

    /// <param name="facingDegrees">0 = facing +Z.</param>
    /// <param name="bob">Small vertical offset for walking/working; 0 under reduced motion. Not used when the elf is animated.</param>
    public void Draw(RunState state, float facingDegrees, float bob, ElfPose pose)
    {
        var p = world.ToWorld(state.PlayerPosition);

        Raylib.DrawCylinder(p + new Vector3(0, 0.01f, 0), 0.32f, 0.32f, 0.01f, 16, Palette.Shadow);

        float carryHeight = CarryHeight;
        if (assets.TryGetModel(AssetLibrary.ElfModel, out var model))
        {
            _modelTop ??= Raylib.GetModelBoundingBox(model).Max.Y; // measured in the rest pose, before any animation
            _animator ??= new ElfAnimator(model, assets.Animations(AssetLibrary.ElfModel));
            if (_animator.IsAnimated)
            {
                _animator.Update(model, Raylib.GetFrameTime(), pose);
                bob = 0; // the animation has its own bounce
            }

            var at = p + new Vector3(0, bob, 0);
            Raylib.DrawModelEx(model, at, Vector3.UnitY, facingDegrees, Vector3.One, Color.White);
            carryHeight = _modelTop.Value + 0.08f; // carry above the hat, whatever height the elf model is

            if (pose.Hammering) DrawHammerInHand(model, at, facingDegrees);
        }
        else
        {
            Rlgl.PushMatrix();
            Rlgl.Translatef(p.X, p.Y + bob, p.Z);
            Rlgl.Rotatef(facingDegrees, 0, 1, 0);
            DrawGreyBoxElf();
            Rlgl.PopMatrix();
        }

        // While building, the toy sits on the bench instead (see WorldView).
        if (state.CarriedItem != null && state.Assembly == null)
            items.Draw(state.CarriedItem, p + new Vector3(0, carryHeight + bob, 0), CarriedScale);
    }

    private void DrawHammerInHand(Model elf, Vector3 at, float facingDegrees)
    {
        if (_animator == null || !assets.TryGetModel(AssetLibrary.HammerModel, out var hammer)) return;
        if (!_animator.HammerTransform(elf, out var local)) return;
        var elfWorld = Raymath.MatrixMultiply(Raymath.MatrixRotateY(facingDegrees * MathF.PI / 180f), Raymath.MatrixTranslate(at.X, at.Y, at.Z));
        hammer.Transform = Raymath.MatrixMultiply(local, elfWorld);
        Raylib.DrawModel(hammer, Vector3.Zero, 1f, Color.White);
    }

    /// <summary>A tired little elf made of primitives, facing +Z.</summary>
    private static void DrawGreyBoxElf()
    {
        Raylib.DrawCylinder(new Vector3(-0.09f, 0, 0), 0.06f, 0.06f, 0.22f, 8, Palette.Ink);   // legs
        Raylib.DrawCylinder(new Vector3(0.09f, 0, 0), 0.06f, 0.06f, 0.22f, 8, Palette.Ink);
        Raylib.DrawCylinder(new Vector3(0, 0.2f, 0), 0.24f, 0.18f, 0.5f, 12, Palette.Pine);    // tunic
        Raylib.DrawCylinder(new Vector3(0, 0.42f, 0), 0.215f, 0.215f, 0.06f, 12, Palette.Gold); // belt
        Raylib.DrawSphere(new Vector3(0, 0.88f, 0), 0.2f, Palette.Skin);                        // head
        Raylib.DrawSphere(new Vector3(-0.07f, 0.9f, 0.17f), 0.03f, Palette.Ink);               // eyes show facing
        Raylib.DrawSphere(new Vector3(0.07f, 0.9f, 0.17f), 0.03f, Palette.Ink);
        Raylib.DrawCylinderEx(new Vector3(-0.18f, 0.88f, 0), new Vector3(-0.3f, 0.95f, 0), 0.05f, 0.0f, 6, Palette.Skin); // ears
        Raylib.DrawCylinderEx(new Vector3(0.18f, 0.88f, 0), new Vector3(0.3f, 0.95f, 0), 0.05f, 0.0f, 6, Palette.Skin);
        Raylib.DrawCylinder(new Vector3(0, 1.0f, 0), 0.0f, 0.2f, 0.32f, 12, Palette.Berry);     // hat
        Raylib.DrawSphere(new Vector3(0, 1.33f, 0), 0.05f, Palette.Snow);
    }
}
