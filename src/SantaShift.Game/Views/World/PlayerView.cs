using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.World;

/// <summary>Draws the elf, its shadow and the carried item overhead.</summary>
public sealed class PlayerView(IAssetLibrary assets, WorldSpace world, ItemView items)
{
    public const float CarryHeight = 1.4f;

    /// <summary>Carried items are drawn smaller so they do not block the view ahead.</summary>
    public const float CarriedScale = 0.75f;

    /// <param name="facingDegrees">0 = facing the camera.</param>
    /// <param name="bob">Small vertical offset for walking/working; 0 under reduced motion.</param>
    public void Draw(RunState state, float facingDegrees, float bob)
    {
        var p = world.ToWorld(state.PlayerPosition);

        Raylib.DrawCylinder(p + new Vector3(0, 0.01f, 0), 0.32f, 0.32f, 0.01f, 16, Palette.Shadow);

        float carryHeight = CarryHeight;
        if (assets.TryGetModel(AssetLibrary.ElfModel, out var model))
        {
            Raylib.DrawModelEx(model, p + new Vector3(0, bob, 0), Vector3.UnitY, facingDegrees, Vector3.One, Color.White);
            carryHeight = ModelTop(model) + 0.08f; // carry above the hat, whatever height the elf model is
        }
        else
        {
            Rlgl.PushMatrix();
            Rlgl.Translatef(p.X, p.Y + bob, p.Z);
            Rlgl.Rotatef(facingDegrees, 0, 1, 0);
            DrawGreyBoxElf();
            Rlgl.PopMatrix();
        }

        if (state.CarriedItem != null)
            items.Draw(state.CarriedItem, p + new Vector3(0, carryHeight + bob, 0), CarriedScale);
    }

    private float? _modelTop;

    private float ModelTop(Model model) => _modelTop ??= Raylib.GetModelBoundingBox(model).Max.Y;

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
