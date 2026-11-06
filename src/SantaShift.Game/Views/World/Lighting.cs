using System.Numerics;
using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.App;

namespace SantaShift.Game.Views.World;

/// <summary>
/// Source-based lighting: the light in the room comes from its fireplace, lanterns, windows and tree
/// (point lights from room_layout.json), plus soft ambient light and glowing materials.
/// One forward pass: per-pixel point lights with smooth falloff, warm/cool ambient, emission, tone mapping and haze.
/// No shadow maps or extra passes, so it stays cheap; the quality setting limits how many lights are used.
/// If the shader cannot compile, models fall back to Raylib's unlit default.
/// </summary>
public sealed class Lighting : IDisposable
{
    public const int MaxLights = 16;

    private const string VertexShader = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec3 vertexNormal;
        in vec4 vertexColor;
        uniform mat4 mvp;
        uniform mat4 matModel;
        uniform mat4 matNormal;
        out vec3 fragPos;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        out vec3 fragNormal;
        void main()
        {
            fragPos = vec3(matModel * vec4(vertexPosition, 1.0));
            fragTexCoord = vertexTexCoord;
            fragColor = vertexColor;
            fragNormal = normalize(vec3(matNormal * vec4(vertexNormal, 0.0)));
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;

    private const string FragmentShader = """
        #version 330
        #define MAX_LIGHTS 16
        in vec3 fragPos;
        in vec2 fragTexCoord;
        in vec4 fragColor;
        in vec3 fragNormal;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform vec4 colEmission;          // the material's glow (filled from the glTF emissive colour)
        uniform int lightCount;
        uniform vec3 lightPos[MAX_LIGHTS];
        uniform vec3 lightColor[MAX_LIGHTS]; // colour x intensity x flicker
        uniform float lightRadius[MAX_LIGHTS];
        uniform vec3 ambientSky;
        uniform vec3 ambientGround;
        uniform vec3 dirLightDir;
        uniform vec3 dirLightColor;
        uniform vec3 viewPos;
        uniform vec3 fogColor;
        uniform float fogDensity;
        uniform float exposure;
        uniform float emissiveBoost;
        out vec4 finalColor;
        void main()
        {
            vec4 base = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            vec3 n = normalize(fragNormal);

            // Soft ambient: a little warmer from below (firelit floor), cooler from above.
            vec3 light = mix(ambientGround, ambientSky, n.y * 0.5 + 0.5);
            light += dirLightColor * max(dot(n, -dirLightDir), 0.0);

            for (int i = 0; i < MAX_LIGHTS; i++)
            {
                if (i >= lightCount) break;
                vec3 toLight = lightPos[i] - fragPos;
                float d = length(toLight);
                float r = lightRadius[i];
                if (d >= r) continue;
                vec3 l = toLight / max(d, 0.0001);
                float falloff = 1.0 - d / r;
                falloff *= falloff;                                 // fades smoothly to zero at the radius
                float wrap = max((dot(n, l) + 0.3) / 1.3, 0.0);     // soft wrap, so shapes stay readable
                light += lightColor[i] * wrap * falloff;
            }

            vec3 c = base.rgb * light + colEmission.rgb * emissiveBoost;
            c = vec3(1.0) - exp(-c * exposure);                     // filmic tone map: bright lights roll off softly

            float fog = 1.0 - exp(-length(viewPos - fragPos) * fogDensity);
            c = mix(c, fogColor, clamp(fog, 0.0, 1.0));             // warm haze gives the cabin depth
            finalColor = vec4(c, base.a);
        }
        """;

    private const string PostShader = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform float vignette;
        uniform float saturation;
        out vec4 finalColor;
        void main()
        {
            vec4 c = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            vec2 uv = fragTexCoord - 0.5;
            float v = clamp(1.0 - vignette * dot(uv, uv) * 2.2, 0.0, 1.0);
            float luma = dot(c.rgb, vec3(0.299, 0.587, 0.114));
            c.rgb = mix(vec3(luma), c.rgb, saturation);
            finalColor = vec4(c.rgb * v, c.a);
        }
        """;

    private readonly LightingSettings _cfg;
    private readonly IReadOnlyList<RoomLight> _lights;
    private readonly int _maxLights;
    private readonly float[] _pos = new float[MaxLights * 3];
    private readonly float[] _color = new float[MaxLights * 3];
    private readonly float[] _radius = new float[MaxLights];
    private readonly int[] _order;
    private readonly int _locCount, _locPos, _locColor, _locRadius, _locView;

    public Shader Shader { get; }
    public Shader Post { get; }
    public bool IsValid { get; }
    public bool UsePost { get; }

    public Lighting(LightingSettings cfg, IReadOnlyList<RoomLight> lights)
    {
        _cfg = cfg;
        _lights = lights;
        _order = Enumerable.Range(0, lights.Count).ToArray();
        _maxLights = Math.Clamp(cfg.MaxLightsForQuality(), 0, MaxLights);

        Shader = Raylib.LoadShaderFromMemory(VertexShader, FragmentShader);
        IsValid = Raylib.IsShaderValid(Shader);
        Post = Raylib.LoadShaderFromMemory(null!, PostShader);
        UsePost = cfg.PostEffectsForQuality() && Raylib.IsShaderValid(Post);
        if (!IsValid) return;

        _locCount = Raylib.GetShaderLocation(Shader, "lightCount");
        _locPos = Raylib.GetShaderLocation(Shader, "lightPos");
        _locColor = Raylib.GetShaderLocation(Shader, "lightColor");
        _locRadius = Raylib.GetShaderLocation(Shader, "lightRadius");
        _locView = Raylib.GetShaderLocation(Shader, "viewPos");

        bool hasRoomLights = lights.Count > 0;
        SetVec3("ambientSky", hasRoomLights ? cfg.AmbientSky : cfg.FallbackAmbient);
        SetVec3("ambientGround", hasRoomLights ? cfg.AmbientGround : cfg.FallbackAmbient);
        SetVec3("dirLightDir", Vector3.Normalize(cfg.DirectionalDirection.ToVector()));
        SetVec3("dirLightColor", hasRoomLights ? cfg.DirectionalColor : cfg.FallbackDirectionalColor);
        SetVec3("fogColor", cfg.FogColor);
        SetFloat("fogDensity", cfg.FogForQuality() ? cfg.FogDensity : 0f);
        SetFloat("exposure", cfg.Exposure);
        SetFloat("emissiveBoost", cfg.EmissiveBoost);

        if (UsePost)
        {
            Raylib.SetShaderValue(Post, Raylib.GetShaderLocation(Post, "vignette"), cfg.Vignette, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(Post, Raylib.GetShaderLocation(Post, "saturation"), cfg.Saturation, ShaderUniformDataType.Float);
        }
    }

    /// <summary>
    /// Makes every material of a model use this lighting, and moves its glTF emissive colour into the slot
    /// Raylib uploads for us each draw (the specular colour, read by the shader as colEmission).
    /// </summary>
    public unsafe void Apply(Model model)
    {
        if (!IsValid) return;
        Shader.Locs[(int)ShaderLocationIndex.ColorSpecular] = Raylib.GetShaderLocation(Shader, "colEmission");
        for (int i = 0; i < model.MaterialCount; i++)
        {
            var maps = model.Materials[i].Maps;
            maps[(int)MaterialMapIndex.Specular].Color = maps[(int)MaterialMapIndex.Emission].Color;
            model.Materials[i].Shader = Shader;
        }
    }

    /// <summary>Picks the lights nearest the camera, applies flicker, and uploads them. Call once per frame.</summary>
    /// <param name="focus">The point the camera looks at (the elf in play). A soft fill light sits between it and the camera.</param>
    public void Update(double time, Vector3 cameraPosition, Vector3 focus, bool reducedMotion)
    {
        if (!IsValid) return;

        bool fill = _cfg.PlayerFill > 0 && _maxLights > 0;
        int roomSlots = fill ? _maxLights - 1 : _maxLights;
        int count = Math.Min(roomSlots, _lights.Count);
        if (count < _lights.Count)
            Array.Sort(_order, (a, b) => Score(a, cameraPosition).CompareTo(Score(b, cameraPosition)));

        for (int k = 0; k < count; k++)
        {
            var l = _lights[_order[k]];
            float f = reducedMotion ? 1f : 1f + (float)l.Flicker * 0.5f * Noise(time, _order[k]);
            float intensity = (float)l.Intensity * f;
            _pos[k * 3] = (float)l.X; _pos[k * 3 + 1] = (float)l.Y; _pos[k * 3 + 2] = (float)l.Z;
            _color[k * 3] = (float)l.Color[0] * intensity;
            _color[k * 3 + 1] = (float)l.Color[1] * intensity;
            _color[k * 3 + 2] = (float)l.Color[2] * intensity;
            _radius[k] = (float)l.Radius;
        }

        if (fill)
        {
            var p = focus + (cameraPosition - focus) * 0.35f + new Vector3(0, 0.4f, 0);
            var c = _cfg.PlayerFillColor;
            _pos[count * 3] = p.X; _pos[count * 3 + 1] = p.Y; _pos[count * 3 + 2] = p.Z;
            _color[count * 3] = c[0] * _cfg.PlayerFill;
            _color[count * 3 + 1] = c[1] * _cfg.PlayerFill;
            _color[count * 3 + 2] = c[2] * _cfg.PlayerFill;
            _radius[count] = 3.5f;
            count++;
        }

        Raylib.SetShaderValue(Shader, _locCount, count, ShaderUniformDataType.Int);
        if (count > 0)
        {
            Raylib.SetShaderValueV(Shader, _locPos, _pos, ShaderUniformDataType.Vec3, count);
            Raylib.SetShaderValueV(Shader, _locColor, _color, ShaderUniformDataType.Vec3, count);
            Raylib.SetShaderValueV(Shader, _locRadius, _radius, ShaderUniformDataType.Float, count);
        }
        Raylib.SetShaderValue(Shader, _locView, cameraPosition, ShaderUniformDataType.Vec3);
    }

    /// <summary>Lower = more important: distance compared with how far and how brightly the light reaches.</summary>
    private float Score(int i, Vector3 p)
    {
        var l = _lights[i];
        float d = Vector3.Distance(new Vector3((float)l.X, (float)l.Y, (float)l.Z), p);
        return d / (float)(l.Radius * Math.Sqrt(l.Intensity));
    }

    /// <summary>Smooth -1..1 waver made from a few sine waves; each light gets its own pattern.</summary>
    private static float Noise(double t, int seed) =>
        (float)(0.55 * Math.Sin(t * 9.1 + seed * 1.7) + 0.3 * Math.Sin(t * 17.3 + seed * 3.1) + 0.15 * Math.Sin(t * 31.7 + seed));

    private void SetVec3(string name, float[] v) =>
        Raylib.SetShaderValue(Shader, Raylib.GetShaderLocation(Shader, name), new Vector3(v[0], v[1], v[2]), ShaderUniformDataType.Vec3);

    private void SetVec3(string name, Vector3 v) =>
        Raylib.SetShaderValue(Shader, Raylib.GetShaderLocation(Shader, name), v, ShaderUniformDataType.Vec3);

    private void SetFloat(string name, float v) =>
        Raylib.SetShaderValue(Shader, Raylib.GetShaderLocation(Shader, name), v, ShaderUniformDataType.Float);

    public void Dispose()
    {
        if (IsValid) Raylib.UnloadShader(Shader);
        if (Raylib.IsShaderValid(Post)) Raylib.UnloadShader(Post);
    }
}
