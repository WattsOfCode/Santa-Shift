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

    // The same, for rigged models: each vertex follows its bones on the GPU (Raylib uploads boneMatrices per draw),
    // so animating an elf costs almost nothing on the CPU however many elves share the model.
    private const string SkinnedVertexShader = """
        #version 330
        #define MAX_BONES 64
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec3 vertexNormal;
        in vec4 vertexColor;
        in vec4 vertexBoneIndices;
        in vec4 vertexBoneWeights;
        uniform mat4 mvp;
        uniform mat4 matModel;
        uniform mat4 matNormal;
        uniform mat4 boneMatrices[MAX_BONES];
        out vec3 fragPos;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        out vec3 fragNormal;
        void main()
        {
            ivec4 b = ivec4(vertexBoneIndices);
            vec4 w = vertexBoneWeights;
            mat4 skin = boneMatrices[b.x] * w.x + boneMatrices[b.y] * w.y + boneMatrices[b.z] * w.z + boneMatrices[b.w] * w.w;
            vec4 pos = skin * vec4(vertexPosition, 1.0);
            vec3 normal = mat3(skin) * vertexNormal;
            fragPos = vec3(matModel * pos);
            fragTexCoord = vertexTexCoord;
            fragColor = vertexColor;
            fragNormal = normalize(vec3(matNormal * vec4(normal, 0.0)));
            gl_Position = mvp * pos;
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
        uniform vec2 texel;     // one pixel of the 3D picture, in texture coordinates
        uniform float fxaa;     // 1 = smooth jagged edges
        out vec4 finalColor;

        // FXAA (fast approximate anti-aliasing): where the brightness changes sharply, blend along the edge instead
        // of across it, so stair-stepped edges and corners stop crawling as the camera moves.
        vec3 smoothEdges(vec2 uv)
        {
            const vec3 L = vec3(0.299, 0.587, 0.114);
            vec3 m = texture(texture0, uv).rgb;
            float lNW = dot(texture(texture0, uv + vec2(-1.0, -1.0) * texel).rgb, L);
            float lNE = dot(texture(texture0, uv + vec2( 1.0, -1.0) * texel).rgb, L);
            float lSW = dot(texture(texture0, uv + vec2(-1.0,  1.0) * texel).rgb, L);
            float lSE = dot(texture(texture0, uv + vec2( 1.0,  1.0) * texel).rgb, L);
            float lM = dot(m, L);
            float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
            float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));
            if (lMax - lMin < max(0.03, lMax * 0.125)) return m; // no edge here
            vec2 dir = vec2(-((lNW + lNE) - (lSW + lSE)), (lNW + lSW) - (lNE + lSE));
            float reduce = max((lNW + lNE + lSW + lSE) * 0.03125, 1.0 / 128.0);
            float scale = 1.0 / (min(abs(dir.x), abs(dir.y)) + reduce);
            dir = clamp(dir * scale, vec2(-8.0), vec2(8.0)) * texel;
            vec3 a = 0.5 * (texture(texture0, uv + dir * (1.0 / 3.0 - 0.5)).rgb + texture(texture0, uv + dir * (2.0 / 3.0 - 0.5)).rgb);
            vec3 b = a * 0.5 + 0.25 * (texture(texture0, uv - dir * 0.5).rgb + texture(texture0, uv + dir * 0.5).rgb);
            float lB = dot(b, L);
            return (lB < lMin || lB > lMax) ? a : b;
        }

        void main()
        {
            vec4 c = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            if (fxaa > 0.5) c.rgb = smoothEdges(fragTexCoord) * colDiffuse.rgb * fragColor.rgb;
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
    private readonly ShaderSlots[] _shaders;

    /// <summary>A lit shader and its per-frame uniform locations.</summary>
    private sealed record ShaderSlots(Shader Shader, int Count, int Pos, int Color, int Radius, int View);

    /// <summary>The lit shader for still models.</summary>
    public Shader Shader { get; }

    /// <summary>The lit shader for rigged models (GPU skinning). Same look as <see cref="Shader"/>.</summary>
    public Shader Skinned { get; }

    public Shader Post { get; }
    public bool IsValid { get; }
    public bool SkinsOnGpu { get; }
    public bool UsePost { get; }

    public Lighting(LightingSettings cfg, IReadOnlyList<RoomLight> lights)
    {
        _cfg = cfg;
        _lights = lights;
        _order = Enumerable.Range(0, lights.Count).ToArray();
        _maxLights = Math.Clamp(cfg.MaxLightsForQuality(), 0, MaxLights);

        Shader = Raylib.LoadShaderFromMemory(VertexShader, FragmentShader);
        IsValid = Raylib.IsShaderValid(Shader);
        Skinned = IsValid ? Raylib.LoadShaderFromMemory(SkinnedVertexShader, FragmentShader) : default;
        SkinsOnGpu = IsValid && Raylib.IsShaderValid(Skinned);
        Post = Raylib.LoadShaderFromMemory(null!, PostShader);
        UsePost = Raylib.IsShaderValid(Post); // always: it also smooths edges; vignette and grading follow the quality
        if (UsePost)
        {
            bool grade = cfg.PostEffectsForQuality();
            Raylib.SetShaderValue(Post, Raylib.GetShaderLocation(Post, "vignette"), grade ? cfg.Vignette : 0f, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(Post, Raylib.GetShaderLocation(Post, "saturation"), grade ? cfg.Saturation : 1f, ShaderUniformDataType.Float);
            _locTexel = Raylib.GetShaderLocation(Post, "texel");
            _locFxaa = Raylib.GetShaderLocation(Post, "fxaa");
        }
        if (!IsValid) { _shaders = []; return; }

        _shaders = SkinsOnGpu ? [Slots(Shader), Slots(Skinned)] : [Slots(Shader)];
        bool hasRoomLights = lights.Count > 0;
        foreach (var s in _shaders)
        {
            SetVec3(s.Shader, "ambientSky", hasRoomLights ? cfg.AmbientSky : cfg.FallbackAmbient);
            SetVec3(s.Shader, "ambientGround", hasRoomLights ? cfg.AmbientGround : cfg.FallbackAmbient);
            SetVec3(s.Shader, "dirLightDir", Vector3.Normalize(cfg.DirectionalDirection.ToVector()));
            SetVec3(s.Shader, "dirLightColor", hasRoomLights ? cfg.DirectionalColor : cfg.FallbackDirectionalColor);
            SetVec3(s.Shader, "fogColor", cfg.FogColor);
            SetFloat(s.Shader, "fogDensity", cfg.FogForQuality() ? cfg.FogDensity : 0f);
            SetFloat(s.Shader, "exposure", cfg.Exposure);
            SetFloat(s.Shader, "emissiveBoost", cfg.EmissiveBoost);
        }

    }

    private readonly int _locTexel = -1, _locFxaa = -1;

    /// <summary>Before drawing the 3D picture to the screen: its size (for edge smoothing) and whether to smooth edges.</summary>
    public void SetPostFrame(int width, int height, bool smoothEdges)
    {
        if (!UsePost) return;
        Raylib.SetShaderValue(Post, _locTexel, new Vector2(1f / width, 1f / height), ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(Post, _locFxaa, smoothEdges ? 1f : 0f, ShaderUniformDataType.Float);
    }

    private static unsafe ShaderSlots Slots(Shader s)
    {
        // The glTF emissive colour is moved into the specular slot (see Apply), which Raylib uploads for us.
        s.Locs[(int)ShaderLocationIndex.ColorSpecular] = Raylib.GetShaderLocation(s, "colEmission");
        return new ShaderSlots(s, Raylib.GetShaderLocation(s, "lightCount"), Raylib.GetShaderLocation(s, "lightPos"),
            Raylib.GetShaderLocation(s, "lightColor"), Raylib.GetShaderLocation(s, "lightRadius"), Raylib.GetShaderLocation(s, "viewPos"));
    }

    /// <summary>
    /// Makes every material of a model use this lighting (the skinned variant for rigged models), and moves its glTF
    /// emissive colour into the slot Raylib uploads for us each draw (the specular colour, read by the shader as colEmission).
    /// </summary>
    public unsafe void Apply(Model model)
    {
        if (!IsValid) return;
        bool rigged = SkinsOnGpu && model.Skeleton.BoneCount > 0 && UploadBoneBuffers(model);
        if (rigged) SkeletonPoser.UseGpuSkinning(model);
        for (int i = 0; i < model.MaterialCount; i++)
        {
            var maps = model.Materials[i].Maps;
            maps[(int)MaterialMapIndex.Specular].Color = maps[(int)MaterialMapIndex.Emission].Color;
            model.Materials[i].Shader = rigged ? Skinned : Shader;
        }
    }

    /// <summary>
    /// Raylib's glTF loader keeps each vertex's bone indices and weights on the CPU only (it skins there). GPU skinning
    /// needs them on the GPU too: upload them into each mesh's vertex array at the skinned shader's attribute slots.
    /// The buffers go in the mesh's own VBO slots, so unloading the model frees them. False if a mesh lacks bone data.
    /// </summary>
    private unsafe bool UploadBoneBuffers(Model model)
    {
        int idsAt = Skinned.Locs[(int)ShaderLocationIndex.VertexBoneIds];
        int weightsAt = Skinned.Locs[(int)ShaderLocationIndex.VertexBoneWeights];
        if (idsAt < 0 || weightsAt < 0) return false;
        for (int m = 0; m < model.MeshCount; m++)
            if (model.Meshes[m].BoneIndices == null || model.Meshes[m].BoneWeights == null || model.Meshes[m].VaoId == 0) return false;

        for (int m = 0; m < model.MeshCount; m++)
        {
            ref var mesh = ref model.Meshes[m];
            if (mesh.VboId[BoneIdsVbo] != 0) continue; // already there (shared mesh, or a newer Raylib uploads them)
            Rlgl.EnableVertexArray(mesh.VaoId);
            mesh.VboId[BoneIdsVbo] = Rlgl.LoadVertexBuffer(mesh.BoneIndices, mesh.VertexCount * 4 * ElementSize(mesh.BoneIndices), false);
            Rlgl.SetVertexAttribute((uint)idsAt, 4, GlTypeOf(mesh.BoneIndices), false, 0, 0);
            Rlgl.EnableVertexAttribute((uint)idsAt);
            mesh.VboId[BoneWeightsVbo] = Rlgl.LoadVertexBuffer(mesh.BoneWeights, mesh.VertexCount * 4 * sizeof(float), false);
            Rlgl.SetVertexAttribute((uint)weightsAt, 4, GlFloat, false, 0, 0);
            Rlgl.EnableVertexAttribute((uint)weightsAt);
            Rlgl.DisableVertexArray();
        }
        return true;
    }

    // Mesh VBO slots Raylib reserves for bone indices and weights (it frees every slot on unload).
    private const int BoneIdsVbo = 7, BoneWeightsVbo = 8;
    private const int GlUnsignedByte = 0x1401, GlUnsignedShort = 0x1403, GlFloat = 0x1406;

    private static unsafe int ElementSize<T>(T* _) where T : unmanaged => sizeof(T);
    private static unsafe int GlTypeOf<T>(T* _) where T : unmanaged => sizeof(T) == 1 ? GlUnsignedByte : GlUnsignedShort;

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

        foreach (var s in _shaders)
        {
            Raylib.SetShaderValue(s.Shader, s.Count, count, ShaderUniformDataType.Int);
            if (count > 0)
            {
                Raylib.SetShaderValueV(s.Shader, s.Pos, _pos, ShaderUniformDataType.Vec3, count);
                Raylib.SetShaderValueV(s.Shader, s.Color, _color, ShaderUniformDataType.Vec3, count);
                Raylib.SetShaderValueV(s.Shader, s.Radius, _radius, ShaderUniformDataType.Float, count);
            }
            Raylib.SetShaderValue(s.Shader, s.View, cameraPosition, ShaderUniformDataType.Vec3);
        }
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

    private static void SetVec3(Shader s, string name, float[] v) =>
        Raylib.SetShaderValue(s, Raylib.GetShaderLocation(s, name), new Vector3(v[0], v[1], v[2]), ShaderUniformDataType.Vec3);

    private static void SetVec3(Shader s, string name, Vector3 v) =>
        Raylib.SetShaderValue(s, Raylib.GetShaderLocation(s, name), v, ShaderUniformDataType.Vec3);

    private static void SetFloat(Shader s, string name, float v) =>
        Raylib.SetShaderValue(s, Raylib.GetShaderLocation(s, name), v, ShaderUniformDataType.Float);

    public void Dispose()
    {
        if (IsValid) Raylib.UnloadShader(Shader);
        if (SkinsOnGpu) Raylib.UnloadShader(Skinned);
        if (Raylib.IsShaderValid(Post)) Raylib.UnloadShader(Post);
    }
}
