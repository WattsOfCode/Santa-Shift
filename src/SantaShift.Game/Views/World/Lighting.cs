using System.Numerics;
using Raylib_cs;

namespace SantaShift.Game.Views.World;

/// <summary>
/// One cheap directional light plus ambient, applied to Blender models so their shape reads in 3D.
/// No shadows, no per-pixel lights beyond one dot product, so it stays light on weak devices.
/// If the shader cannot compile, models simply stay unlit.
/// </summary>
public sealed class Lighting : IDisposable
{
    private const string VertexShader = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec3 vertexNormal;
        in vec4 vertexColor;
        uniform mat4 mvp;
        uniform mat4 matNormal;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        out vec3 fragNormal;
        void main()
        {
            fragTexCoord = vertexTexCoord;
            fragColor = vertexColor;
            fragNormal = normalize(vec3(matNormal * vec4(vertexNormal, 0.0)));
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;

    private const string FragmentShader = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        in vec3 fragNormal;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform vec3 lightDir;
        uniform vec3 lightColor;
        uniform vec3 ambient;
        out vec4 finalColor;
        void main()
        {
            vec4 base = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            float diffuse = max(dot(normalize(fragNormal), -lightDir), 0.0);
            finalColor = vec4(base.rgb * (ambient + lightColor * diffuse), base.a);
        }
        """;

    public Shader Shader { get; }
    public bool IsValid { get; }

    public Lighting()
    {
        Shader = Raylib.LoadShaderFromMemory(VertexShader, FragmentShader);
        IsValid = Raylib.IsShaderValid(Shader);
        if (!IsValid) return;

        Set("lightDir", Vector3.Normalize(new Vector3(-0.35f, -1f, -0.45f)));
        Set("lightColor", new Vector3(0.55f, 0.52f, 0.48f));
        Set("ambient", new Vector3(0.58f, 0.6f, 0.66f));
    }

    /// <summary>Makes every material of a model use this light.</summary>
    public unsafe void Apply(Model model)
    {
        if (!IsValid) return;
        for (int i = 0; i < model.MaterialCount; i++) model.Materials[i].Shader = Shader;
    }

    private void Set(string name, Vector3 value) =>
        Raylib.SetShaderValue(Shader, Raylib.GetShaderLocation(Shader, name), value, ShaderUniformDataType.Vec3);

    public void Dispose()
    {
        if (IsValid) Raylib.UnloadShader(Shader);
    }
}
