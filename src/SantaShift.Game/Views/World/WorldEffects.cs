using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Game.Services;

namespace SantaShift.Game.Views.World;

/// <summary>
/// Soft light effects drawn over the lit scene, cheaply: halos around the tree's bulbs and star (room_layout.json >
/// glows), twinkling like old incandescent lights, and snow flurrying past the windows now and then.
/// All camera-facing sprites from one small generated texture, drawn after the solid scene, so they cost almost nothing.
/// </summary>
public sealed class WorldEffects : IDisposable
{
    /// <summary>Set each frame from the settings: no twinkle, and the window snow hangs still.</summary>
    public static bool ReducedMotion { get; set; }

    // The window model (prop_window): the glass is 1.3 x 1.1 m, its bottom 1.35 m up, facing the room (local +Z).
    private const string WindowModel = "prop_window";
    private const float PaneHalfWidth = 0.6f, PaneBottom = 1.4f, PaneTop = 2.4f, PaneDepth = 0.07f;
    private const int FlakesPerWindow = 18;

    private readonly GameContent _content;
    private readonly Texture2D _soft;
    private readonly bool _hasTexture;

    public WorldEffects(GameContent content)
    {
        _content = content;
        // A round, soft-edged dot: white in the middle, fading to nothing.
        var image = Raylib.GenImageGradientRadial(64, 64, 0f, Color.White, new Color(255, 255, 255, 0));
        _soft = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);
        _hasTexture = Raylib.IsTextureValid(_soft);
        if (_hasTexture) Raylib.SetTextureFilter(_soft, TextureFilter.Bilinear);
    }

    /// <summary>Call inside the 3D pass, after everything solid.</summary>
    public void Draw(WorldSpace world, double time)
    {
        if (!_hasTexture) return;
        float t = ReducedMotion ? 0 : (float)time;
        var camera = world.Camera;

        Rlgl.DisableDepthMask(); // see-through sprites: hidden behind solid things, but never hide each other
        Raylib.BeginBlendMode(BlendMode.Additive);
        var glows = _content.Glows;
        for (int i = 0; i < glows.Count; i++)
        {
            var g = glows[i];
            var p = new Vector3((float)g.X, (float)g.Y, (float)g.Z);
            // Each bulb has its own slow breathing plus a quick shimmer, like a warm filament.
            float wave = ReducedMotion ? 0.8f
                : 0.62f + 0.28f * MathF.Sin(t * (1.3f + Hash(i) * 1.6f) + i * 2.1f) + 0.1f * MathF.Sin(t * 7.3f + i);
            float bright = 1f - (float)g.Twinkle * (1f - wave);
            var c = new Color((byte)(g.Color[0] * 255), (byte)(g.Color[1] * 255), (byte)(g.Color[2] * 255), (byte)(255 * 0.55f * bright));
            float size = (float)g.Size * (0.9f + 0.15f * bright);
            Raylib.DrawBillboard(camera, _soft, p, size, c);
            Raylib.DrawBillboard(camera, _soft, p, size * 0.35f, new Color((byte)255, (byte)250, (byte)235, (byte)(200 * bright))); // hot core
        }
        Raylib.EndBlendMode();

        // Snow past the windows: flurries come and go (a slow swell), the flakes drift down and sideways.
        Raylib.BeginBlendMode(BlendMode.Alpha);
        int w = 0;
        foreach (var prop in _content.Props)
        {
            if (prop.Model != WindowModel) continue;
            float gust = ReducedMotion ? 0.5f : Math.Clamp(0.5f + 0.8f * MathF.Sin(t * 0.23f + w * 1.9f) + 0.3f * MathF.Sin(t * 0.61f + w), 0, 1);
            if (gust <= 0.02f) { w++; continue; }
            var origin = world.ToWorld(prop.Position);
            float yaw = (float)prop.Yaw * MathF.PI / 180f;
            var right = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
            var forward = new Vector3(MathF.Sin(yaw), 0, MathF.Cos(yaw));
            float scale = (float)prop.Scale;
            for (int f = 0; f < FlakesPerWindow; f++)
            {
                float seed = w * 31 + f;
                if (Hash(seed * 1.3f) > gust) continue; // fewer flakes in a lull
                float speed = 0.25f + Hash(seed * 2.7f) * 0.35f;
                float fall = (Hash(seed) + t * speed / (PaneTop - PaneBottom)) % 1f;
                float y = PaneTop - fall * (PaneTop - PaneBottom);
                float x = (Hash(seed * 4.1f) * 2 - 1) * PaneHalfWidth + MathF.Sin(t * 1.4f + seed) * 0.05f + fall * 0.12f;
                if (MathF.Abs(x) > PaneHalfWidth) continue;
                var p = origin + (right * x + forward * PaneDepth) * scale + new Vector3(0, y * scale, 0);
                float size = 0.05f + Hash(seed * 5.9f) * 0.04f;
                byte alpha = (byte)(255 * MathF.Min(1, MathF.Min(fall * 6, (1 - fall) * 6))); // fade in at the top, out at the sill
                Raylib.DrawBillboard(camera, _soft, p, size, new Color((byte)245, (byte)248, (byte)255, alpha));
            }
            w++;
        }
        Raylib.EndBlendMode();
        Rlgl.EnableDepthMask();
    }

    private static float Hash(float v) => MathF.Abs(MathF.Sin(v * 12.9898f + 4.1f) * 43758.5453f) % 1;

    public void Dispose()
    {
        if (_hasTexture) Raylib.UnloadTexture(_soft);
    }
}
