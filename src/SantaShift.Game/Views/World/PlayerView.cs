using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.World;

/// <summary>Draws the elf (animated when elf.glb has actions) with the tool it's using, its shadow, and the carried item.</summary>
public sealed class PlayerView(IAssetLibrary assets, WorldSpace world, ItemView items)
{
    public const float CarryHeight = 1.4f;

    /// <summary>Carried items are drawn smaller so they do not block the view ahead.</summary>
    public const float CarriedScale = 0.75f;

    /// <summary>Held up on the elf's hand, items are a little smaller still, so they fit beside its head.</summary>
    public const float HeldScale = 0.6f;

    private readonly ElfFigure _figure = new(assets);

    /// <summary>The player's hat colour (an unlock), or null for the classic red.</summary>
    public Color? Hat { get => _figure.Hat; set => _figure.Hat = value; }
    private float? _modelTop;

    /// <param name="position">Where to draw the elf (between simulation steps, for smooth motion at high frame rates).</param>
    /// <param name="facingDegrees">0 = facing +Z.</param>
    /// <param name="bob">Small vertical offset for walking/working; 0 under reduced motion. Not used when the elf is animated.</param>
    public void Draw(RunState state, Point2 position, float facingDegrees, float bob, ElfPose pose)
    {
        var p = world.ToWorld(position);

        Raylib.DrawCylinder(p + new Vector3(0, 0.01f, 0), 0.32f, 0.32f, 0.01f, 16, Palette.Shadow);

        float carryHeight = CarryHeight;
        Vector3? held = null;
        if (assets.TryGetModel(AssetLibrary.ElfModel, out var model))
        {
            _modelTop ??= Raylib.GetModelBoundingBox(model).Max.Y; // measured in the rest pose, before any animation
            if (_figure.IsAnimated) bob = 0;                        // the animation has its own bounce
            var at = p + new Vector3(0, bob, 0);
            _figure.Draw(at, facingDegrees, pose with { Carrying = state.CarriedItem != null && !OnStation(state) },
                Raylib.GetFrameTime());
            if (_figure.CanCarry && _figure.TryHoldPoint(at, facingDegrees, out var hand)) held = hand;
            carryHeight = _modelTop.Value + 0.08f; // carry above the hat, whatever height the elf model is
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
        if (state.CarriedItem == null || OnStation(state)) return;
        if (held is { } h) // held up on the right hand; toys are long side-to-side, so their outline shows from behind
            items.Draw(state.CarriedItem, h + new Vector3(0, 0.02f, 0), HeldScale, facingDegrees);
        else // no Carry pose in elf.glb (or no elf model): float it above the head
            items.Draw(state.CarriedItem, p + new Vector3(0, carryHeight + bob, 0), CarriedScale);
    }

    /// <summary>While building or ribboning, the item sits on the station (see WorldView), not in the elf's hand.</summary>
    private static bool OnStation(RunState s) =>
        s.Assembly != null || s.RibbonPick != null || s.CraftingAction is { Type: CraftingType.Ribbon };

    /// <summary>A tired little elf made of primitives, facing +Z.</summary>
    public static void DrawGreyBoxElf()
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
