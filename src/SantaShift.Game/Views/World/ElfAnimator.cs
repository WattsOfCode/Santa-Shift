using System.Numerics;
using Raylib_cs;

namespace SantaShift.Game.Views.World;

/// <summary>What the elf is doing this frame, for its animation.</summary>
/// <param name="Swings">Counts hammer swings; each increase plays one swing.</param>
public readonly record struct ElfPose(bool Walking, bool Hammering, int Swings);

/// <summary>
/// Plays the elf's Blender actions (Idle, Walk, Work) with short crossfades, and works out where the hammer in
/// its right hand is. Work is one hammer swing whose first and last frame is the "ready" pose, held between swings.
/// Without animations in elf.glb this does nothing and the elf is drawn in its rest pose (the view bobs it instead).
/// </summary>
public sealed class ElfAnimator
{
    private enum Clip { Idle, Walk, Work }

    /// <summary>Raylib samples glTF animations at 60 frames per second.</summary>
    private const float FramesPerSecond = 60f;
    private const float BlendSeconds = 0.15f;
    private const string HandBone = "hand.R";

    private readonly ModelAnimation?[] _clips = new ModelAnimation?[3];
    private readonly int _hand = -1;

    /// <summary>Hammer model space (Y up, grip 6 cm up the handle) to the elf's fist in its rest pose:
    /// handle pointing forward, striking face down.</summary>
    private readonly Matrix4x4 _hammerRest = Matrix4x4.Identity;

    private Clip _current = Clip.Idle, _previous = Clip.Idle;
    private float _time, _previousTime, _blend = 1f;
    private int _lastSwings;
    private float _swingTime = float.MaxValue / 2;

    public bool IsAnimated { get; }

    public unsafe ElfAnimator(Model elf, IReadOnlyList<ModelAnimation> animations)
    {
        foreach (var a in animations)
            if (Enum.TryParse<Clip>(a.NameToString(), out var clip)) _clips[(int)clip] = a;
        IsAnimated = _clips.Any(c => c.HasValue);

        for (int i = 0; i < elf.Skeleton.BoneCount; i++)
            if (elf.Skeleton.Bones[i].NameToString() == HandBone) _hand = i;
        if (_hand < 0) return;

        var fist = elf.Skeleton.BindPose[_hand].Translation + new Vector3(0, -0.04f, 0); // 4 cm below the wrist
        _hammerRest = Raymath.MatrixMultiply(
            Raymath.MatrixMultiply(Raymath.MatrixTranslate(0, -0.06f, 0), Raymath.MatrixRotateX(MathF.PI / 2)),
            Raymath.MatrixTranslate(fist.X, fist.Y, fist.Z));
    }

    /// <summary>Advances and applies the pose to <paramref name="elf"/>. Call once per frame before drawing it.</summary>
    public void Update(Model elf, float dt, ElfPose pose)
    {
        if (!IsAnimated) return;

        if (pose.Swings != _lastSwings) { _lastSwings = pose.Swings; _swingTime = 0; }
        var wanted = pose.Hammering && Has(Clip.Work) ? Clip.Work
            : pose.Walking && Has(Clip.Walk) ? Clip.Walk
            : Has(Clip.Idle) ? Clip.Idle : _current;
        if (wanted != _current)
        {
            (_previous, _previousTime) = (_current, _time);
            (_current, _time, _blend) = (wanted, 0, 0);
        }

        _time += dt;
        _previousTime += dt;
        _swingTime += dt;
        _blend = Math.Min(1f, _blend + dt / BlendSeconds);

        if (_clips[(int)_current] is not { } now) return;
        float frame = FrameOf(_current, now, _time);
        if (_blend < 1f && _clips[(int)_previous] is { } before)
            Raylib.UpdateModelAnimationEx(elf, before, FrameOf(_previous, before, _previousTime), now, frame, _blend);
        else
            Raylib.UpdateModelAnimation(elf, now, frame);
    }

    /// <summary>The hammer's transform in the elf's model space for the current pose. False if the rig has no right hand.</summary>
    public unsafe bool HammerTransform(Model elf, out Matrix4x4 local)
    {
        local = default;
        if (_hand < 0 || elf.BoneMatrices == null) return false;
        // A bone matrix takes a rest-pose point on the hand to where it is in the current pose.
        local = Raymath.MatrixMultiply(_hammerRest, elf.BoneMatrices[_hand]);
        return true;
    }

    private bool Has(Clip c) => _clips[(int)c].HasValue;

    /// <summary>Idle and Walk loop. Work plays once per swing, then rests on frame 0 (the ready pose).</summary>
    private float FrameOf(Clip clip, ModelAnimation a, float seconds)
    {
        float last = Math.Max(1, a.KeyFrameCount - 1);
        if (clip != Clip.Work) return seconds * FramesPerSecond % last;
        float f = _swingTime * FramesPerSecond;
        return f >= last ? 0 : f;
    }
}
