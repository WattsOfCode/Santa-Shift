using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.World;

/// <summary>
/// The guided first round's arrows: a trail of low, see-through gold chevrons on the floor from the elf to the next
/// station (around benches and bins, not through them), flowing towards it, with a soft ring where to stand.
/// The elf walks straight through them. The route is re-planned only when the elf moves on a little or the goal changes.
/// </summary>
public sealed class GuideArrowView(GameContent content, IAssetLibrary assets, WorldSpace world)
{
    private const float Spacing = 0.6f;      // metres between arrows
    private const float FlowSpeed = 0.6f;    // metres per second the trail drifts towards the goal
    private const float ClearOfElf = 0.55f;  // no arrow under or right in front of the elf
    private const byte Opacity = 190;
    private const float Size = 1.25f;

    private readonly FloorPath _path = new(content);
    private List<Vector3> _route = [];
    private Point2 _routeFrom;
    private string? _routeTo;

    public void Draw(Point2 player, Station? goal, double time, bool reducedMotion)
    {
        if (goal == null) { _routeTo = null; return; }
        if (_routeTo != goal.Id || _routeFrom.DistanceTo(player) > 10)
        {
            _route = _path.Find(player, goal.Interact).Select(p => world.ToWorld(p)).ToList();
            (_routeFrom, _routeTo) = (player, goal.Id);
        }
        // Start the trail at the elf's actual position, so it never lags behind.
        if (_route.Count > 0) _route[0] = world.ToWorld(player);

        float length = 0;
        for (int i = 0; i + 1 < _route.Count; i++) length += Vector3.Distance(_route[i], _route[i + 1]);
        float flow = reducedMotion ? 0 : (float)(time * FlowSpeed % Spacing);

        Rlgl.DisableDepthMask(); // see-through: they mustn't hide each other
        bool hasModel = assets.TryGetModel(AssetLibrary.GuideArrowModel, out var arrow);
        var lit = hasModel ? UseUnlit(arrow) : null; // they glow their own gold, whatever the room light
        for (float d = ClearOfElf + flow; d < length - 0.3f; d += Spacing)
        {
            var (p, dir) = At(d);
            // Fade in near the elf and out near the goal.
            float fade = Math.Clamp((d - ClearOfElf) / 0.5f, 0, 1) * Math.Clamp((length - d) / 0.6f, 0, 1);
            var tint = new Color((byte)255, (byte)255, (byte)255, (byte)(Opacity * fade));
            float yaw = MathF.Atan2(dir.X, dir.Z) * 180f / MathF.PI;
            if (hasModel) Raylib.DrawModelEx(arrow, p + new Vector3(0, 0.012f, 0), Vector3.UnitY, yaw, new Vector3(Size, 1, Size), tint);
            else DrawFlatChevron(p, dir, tint);
        }

        if (lit != null) Restore(arrow, lit);

        // Where to stand: a soft ring that breathes.
        var spot = world.ToWorld(goal.Interact);
        float pulse = reducedMotion ? 1 : 0.85f + 0.15f * MathF.Sin((float)time * 4);
        Raylib.DrawCylinder(spot + new Vector3(0, 0.01f, 0), 0.34f * pulse, 0.34f * pulse, 0.02f, 24, new Color((byte)255, (byte)214, (byte)102, (byte)70));
        Raylib.DrawCylinderWires(spot + new Vector3(0, 0.012f, 0), 0.36f * pulse, 0.36f * pulse, 0.02f, 24, new Color((byte)255, (byte)230, (byte)150, (byte)170));
        Rlgl.EnableDepthMask();
    }

    /// <summary>Swaps the model onto Raylib's plain unlit shader; returns its own shaders to put back.</summary>
    private static unsafe Shader[] UseUnlit(Model model)
    {
        var plain = new Shader { Id = Rlgl.GetShaderIdDefault(), Locs = Rlgl.GetShaderLocsDefault() };
        var saved = new Shader[model.MaterialCount];
        for (int i = 0; i < model.MaterialCount; i++) { saved[i] = model.Materials[i].Shader; model.Materials[i].Shader = plain; }
        return saved;
    }

    private static unsafe void Restore(Model model, Shader[] saved)
    {
        for (int i = 0; i < model.MaterialCount; i++) model.Materials[i].Shader = saved[i];
    }

    /// <summary>The point and direction <paramref name="distance"/> metres along the route.</summary>
    private (Vector3 Point, Vector3 Direction) At(float distance)
    {
        for (int i = 0; i + 1 < _route.Count; i++)
        {
            var a = _route[i];
            var b = _route[i + 1];
            float seg = Vector3.Distance(a, b);
            if (distance <= seg || i + 2 == _route.Count)
            {
                var dir = seg > 1e-4f ? (b - a) / seg : Vector3.UnitZ;
                return (a + dir * Math.Min(distance, seg), dir);
            }
            distance -= seg;
        }
        return (_route.Count > 0 ? _route[^1] : Vector3.Zero, Vector3.UnitZ);
    }

    /// <summary>Fallback without guide_arrow.glb: a flat chevron from two thin boxes.</summary>
    private static void DrawFlatChevron(Vector3 p, Vector3 dir, Color tint)
    {
        var side = new Vector3(-dir.Z, 0, dir.X);
        var tip = p + dir * 0.14f + new Vector3(0, 0.02f, 0);
        Raylib.DrawLine3D(tip, tip - dir * 0.22f + side * 0.18f, tint);
        Raylib.DrawLine3D(tip, tip - dir * 0.22f - side * 0.18f, tint);
    }
}
