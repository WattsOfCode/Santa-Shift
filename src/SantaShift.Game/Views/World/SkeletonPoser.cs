using System.Numerics;
using Raylib_cs;

namespace SantaShift.Game.Views.World;

/// <summary>
/// Poses a rigged model for one draw. Raylib's UpdateModelAnimation also re-skins every vertex on the CPU and
/// re-uploads the mesh (about 1.7 ms per elf), even when a shader could do it. When the lighting shader skins on the
/// GPU, this only works out the bone matrices (15 for the elf) and the shader does the rest during the draw.
/// Without GPU skinning it falls back to Raylib's CPU path.
/// </summary>
public static class SkeletonPoser
{
    private static readonly HashSet<nint> GpuSkinned = [];

    /// <summary>Marks a model as skinned by its shader (see <see cref="Lighting.Apply"/>); others use Raylib's CPU path.</summary>
    public static unsafe void UseGpuSkinning(Model model) => GpuSkinned.Add((nint)model.Meshes);

    public static unsafe bool IsGpuSkinned(Model model) => GpuSkinned.Contains((nint)model.Meshes);

    /// <summary>
    /// A second action laid over some bones (e.g. the arms holding a carried item while the legs walk). Those bones keep
    /// their place relative to <paramref name="Anchor"/> (the chest), so they ride along with the base pose's bounce.
    /// </summary>
    public readonly record struct Overlay(ModelAnimation Anim, float Frame, float Weight, int[] Bones, int Anchor);

    private static readonly Transform[] Scratch = new Transform[256];

    public static void Pose(Model model, ModelAnimation anim, float frame) => Pose(model, anim, frame, anim, frame, 0f);

    /// <summary>Poses <paramref name="model"/> between two animations (<paramref name="blend"/> 0 = all A, 1 = all B).</summary>
    /// <param name="overlay">Optional; only applied with GPU skinning (the CPU fallback shows the base pose).</param>
    public static unsafe void Pose(Model model, ModelAnimation a, float frameA, ModelAnimation b, float frameB, float blend,
        Overlay? overlay = null)
    {
        if (model.BoneMatrices == null || !IsGpuSkinned(model))
        {
            if (blend <= 0f) Raylib.UpdateModelAnimation(model, a, frameA);
            else Raylib.UpdateModelAnimationEx(model, a, frameA, b, frameB, blend);
            return;
        }

        int bones = Math.Min(model.Skeleton.BoneCount, a.BoneCount);
        if (blend > 0f) bones = Math.Min(bones, b.BoneCount);
        bones = Math.Min(bones, Scratch.Length);
        for (int i = 0; i < bones; i++)
        {
            Scratch[i] = Sample(a, frameA, i);
            if (blend > 0f) Scratch[i] = Mix(Scratch[i], Sample(b, frameB, i), blend);
        }

        if (overlay is { Weight: > 0f } o && o.Anchor >= 0 && o.Anchor < bones && o.Anim.BoneCount >= bones)
        {
            var theirs = Sample(o.Anim, o.Frame, o.Anchor);
            var ours = Scratch[o.Anchor];
            var undo = Quaternion.Inverse(theirs.Rotation);
            foreach (int i in o.Bones)
            {
                if (i < 0 || i >= bones) continue;
                var t = Sample(o.Anim, o.Frame, i);
                // Where the bone is relative to the overlay's anchor, then put back onto the base pose's anchor.
                var relRot = undo * t.Rotation;
                var relPos = Vector3.Transform(t.Translation - theirs.Translation, undo);
                var placed = new Transform
                {
                    Rotation = Quaternion.Normalize(ours.Rotation * relRot),
                    Translation = Vector3.Transform(relPos, ours.Rotation) + ours.Translation,
                    Scale = t.Scale,
                };
                Scratch[i] = Mix(Scratch[i], placed, Math.Min(1f, o.Weight));
            }
        }

        for (int i = 0; i < bones; i++)
            model.BoneMatrices[i] = Raymath.MatrixMultiply(Inverse(model.Skeleton.BindPose[i]), ToMatrix(Scratch[i]));
    }

    /// <summary>A bone's model-space pose at a fractional frame, between the two key frames around it.</summary>
    private static unsafe Transform Sample(ModelAnimation anim, float frame, int bone)
    {
        int last = Math.Max(0, anim.KeyFrameCount - 1);
        frame = Math.Clamp(frame, 0, last);
        int f0 = (int)frame, f1 = Math.Min(f0 + 1, last);
        var p0 = anim.KeyframePoses[f0][bone];
        if (f1 == f0) return p0;
        return Mix(p0, anim.KeyframePoses[f1][bone], frame - f0);
    }

    private static Transform Mix(Transform x, Transform y, float t) => new()
    {
        Translation = Vector3.Lerp(x.Translation, y.Translation, t),
        Rotation = Quaternion.Slerp(x.Rotation, y.Rotation, t),
        Scale = Vector3.Lerp(x.Scale, y.Scale, t),
    };

    private static Matrix4x4 ToMatrix(Transform t) => Raymath.MatrixMultiply(
        Raymath.MatrixMultiply(Raymath.MatrixScale(t.Scale.X, t.Scale.Y, t.Scale.Z), Raymath.QuaternionToMatrix(t.Rotation)),
        Raymath.MatrixTranslate(t.Translation.X, t.Translation.Y, t.Translation.Z));

    private static Matrix4x4 Inverse(Transform t) => Raymath.MatrixInvert(ToMatrix(t));

}
