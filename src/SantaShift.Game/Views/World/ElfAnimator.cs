using System.Numerics;
using Raylib_cs;

namespace SantaShift.Game.Views.World;

/// <summary>What the elf's hands are busy with.</summary>
public enum ElfActivity { None, Hammering, Shovelling }

/// <summary>What the elf is doing this frame, for its animation.</summary>
/// <param name="Swings">Counts hammer swings; each increase plays one swing.</param>
/// <param name="Carrying">Holding an item (a kit, a toy or coal): the arms take the Carry pose over the walk.</param>
/// <param name="Pushing">Pushing the toy cart: both arms take the Push pose (hands on its handle) over the walk.</param>
public readonly record struct ElfPose(bool Walking, ElfActivity Activity, int Swings, bool Carrying = false, bool Pushing = false)
{
    public bool Hammering => Activity == ElfActivity.Hammering;
}

/// <summary>
/// Plays the elf's Blender actions (Idle, Walk, Work, Shovel) with short crossfades, and works out where the tool in
/// its right hand is. Work is one hammer swing whose first and last frame is the "ready" pose, held between swings;
/// Shovel is one scoop, played from the start each time the elf starts shovelling.
/// Without animations in elf.glb this does nothing and the elf is drawn in its rest pose (the view bobs it instead).
/// One animator per elf on screen; several animators can share the elf model (each poses it just before it's drawn).
/// </summary>
public sealed class ElfAnimator
{
    private enum Clip { Idle, Walk, Work, Shovel, Carry, Push }

    /// <summary>Raylib samples glTF animations at 60 frames per second.</summary>
    private const float FramesPerSecond = 60f;
    private const float BlendSeconds = 0.15f;
    private const string HandBone = "hand.R";

    private readonly ModelAnimation?[] _clips = new ModelAnimation?[6];
    private readonly int _hand = -1;
    private readonly int _chest = -1;
    private readonly bool _gpu;
    private readonly int[] _carryBones = [];
    private readonly int[] _pushBones = [];
    private readonly Vector3 _wrist;
    private float _carryWeight, _pushWeight;

    /// <summary>The bones the Push pose takes over: both arms, reaching to the cart's handle. The legs keep walking.</summary>
    private static readonly string[] PushBones = ["upper_arm.L", "forearm.L", "hand.L", "upper_arm.R", "forearm.R", "hand.R"];

    /// <summary>
    /// The bones the Carry pose takes over: the right arm, hoisting the item up beside the head (in front, the elf's big
    /// head would hide it from the camera behind). The rest keep walking or idling.
    /// </summary>
    private static readonly string[] CarryBones = ["upper_arm.R", "forearm.R", "hand.R"];

    // Each tool's model space (Y up, standing on the floor) to the elf's right fist in its rest pose (arm hanging).
    // Hammer: held 6 cm up the handle, pointing forward with the striking face down.
    // Shovel: held 0.62 m up the shaft, pointing forward and down so the blade is out in front.
    private readonly Matrix4x4 _hammerRest = Matrix4x4.Identity, _shovelRest = Matrix4x4.Identity;

    private Clip _current = Clip.Idle, _previous = Clip.Idle;
    private float _time, _previousTime, _blend = 1f;
    private int _lastSwings;
    private float _swingTime = float.MaxValue / 2;

    public bool IsAnimated { get; }

    /// <summary>True when elf.glb has a Carry action (then items are held in the hand, not floated above the head).</summary>
    public bool CanCarry => _hand >= 0 && Has(Clip.Carry) && _gpu; // the arm pose is layered on the GPU path

    public unsafe ElfAnimator(Model elf, IReadOnlyList<ModelAnimation> animations)
    {
        foreach (var a in animations)
            if (Enum.TryParse<Clip>(a.NameToString(), out var clip)) _clips[(int)clip] = a;
        IsAnimated = _clips.Any(c => c.HasValue);
        _gpu = SkeletonPoser.IsGpuSkinned(elf);

        var names = new string[elf.Skeleton.BoneCount];
        for (int i = 0; i < names.Length; i++) names[i] = elf.Skeleton.Bones[i].NameToString();
        _hand = Array.IndexOf(names, HandBone);
        _chest = Array.IndexOf(names, "chest");
        _carryBones = CarryBones.Select(b => Array.IndexOf(names, b)).Where(i => i >= 0).ToArray();
        _pushBones = PushBones.Select(b => Array.IndexOf(names, b)).Where(i => i >= 0).ToArray();
        if (_hand < 0) return;

        _wrist = elf.Skeleton.BindPose[_hand].Translation;
        var fist = elf.Skeleton.BindPose[_hand].Translation + new Vector3(0, -0.04f, 0); // 4 cm below the wrist
        var toFist = Raymath.MatrixTranslate(fist.X, fist.Y, fist.Z);
        _hammerRest = Raymath.MatrixMultiply(
            Raymath.MatrixMultiply(Raymath.MatrixTranslate(0, -0.06f, 0), Raymath.MatrixRotateX(MathF.PI / 2)), toFist);
        _shovelRest = Raymath.MatrixMultiply(
            Raymath.MatrixMultiply(Raymath.MatrixTranslate(0, -0.62f, 0), Raymath.MatrixRotateX(-MathF.PI / 3)), toFist);
    }

    /// <summary>Advances and applies the pose to <paramref name="elf"/>. Call just before drawing it.</summary>
    public void Update(Model elf, float dt, ElfPose pose)
    {
        if (!IsAnimated) return;

        if (pose.Swings != _lastSwings) { _lastSwings = pose.Swings; _swingTime = 0; }
        var wanted = pose.Activity switch
        {
            ElfActivity.Hammering when Has(Clip.Work) => Clip.Work,
            ElfActivity.Shovelling when Has(Clip.Shovel) => Clip.Shovel,
            _ => pose.Walking && Has(Clip.Walk) ? Clip.Walk : Has(Clip.Idle) ? Clip.Idle : _current,
        };
        if (wanted != _current)
        {
            (_previous, _previousTime) = (_current, _time);
            (_current, _time, _blend) = (wanted, 0, 0);
        }

        _time += dt;
        _previousTime += dt;
        _swingTime += dt;
        _blend = Math.Min(1f, _blend + dt / BlendSeconds);
        bool carrying = pose.Carrying && pose.Activity == ElfActivity.None && Has(Clip.Carry);
        bool pushing = pose.Pushing && pose.Activity == ElfActivity.None && Has(Clip.Push);
        _carryWeight = Math.Clamp(_carryWeight + (carrying ? dt : -dt) / BlendSeconds, 0f, 1f);
        _pushWeight = Math.Clamp(_pushWeight + (pushing ? dt : -dt) / BlendSeconds, 0f, 1f);
        // The arms take one layered pose at a time: the cart's handle (pushing means empty hands) or a carried item.
        SkeletonPoser.Overlay? overlay =
            _pushWeight > 0f && _pushWeight >= _carryWeight && _clips[(int)Clip.Push] is { } push
                ? new SkeletonPoser.Overlay(push, _time * FramesPerSecond % Math.Max(1, push.KeyFrameCount - 1), Smooth(_pushWeight), _pushBones, _chest)
            : _carryWeight > 0f && _clips[(int)Clip.Carry] is { } carry
                ? new SkeletonPoser.Overlay(carry, _time * FramesPerSecond % Math.Max(1, carry.KeyFrameCount - 1), Smooth(_carryWeight), _carryBones, _chest)
            : null;

        if (_clips[(int)_current] is not { } now) return;
        float frame = FrameOf(_current, now, _time);
        if (_blend < 1f && _clips[(int)_previous] is { } before)
            SkeletonPoser.Pose(elf, before, FrameOf(_previous, before, _previousTime), now, frame, _blend, overlay);
        else
            SkeletonPoser.Pose(elf, now, frame, now, frame, 0f, overlay);
    }

    /// <summary>The tool's transform in the elf's model space for the current pose. False if the rig has no right hand.</summary>
    public unsafe bool ToolTransform(Model elf, ElfActivity tool, out Matrix4x4 local)
    {
        local = default;
        if (_hand < 0 || elf.BoneMatrices == null || tool == ElfActivity.None) return false;
        // A bone matrix takes a rest-pose point on the hand to where it is in the current pose.
        var rest = tool == ElfActivity.Shovelling ? _shovelRest : _hammerRest;
        local = Raymath.MatrixMultiply(rest, elf.BoneMatrices[_hand]);
        return true;
    }

    /// <summary>
    /// Where a carried item sits, in the elf's model space: on top of its hands. Follows the hands through the Carry pose
    /// (and the walk's bounce). False without a right hand bone.
    /// </summary>
    public unsafe bool HoldPoint(Model elf, out Vector3 local)
    {
        local = default;
        if (_hand < 0 || elf.BoneMatrices == null) return false;
        local = Raymath.Vector3Transform(_wrist, elf.BoneMatrices[_hand]);
        return true;
    }

    private static float Smooth(float t) => t * t * (3 - 2 * t);

    private bool Has(Clip c) => _clips[(int)c].HasValue;

    /// <summary>
    /// Idle and Walk loop. Work plays once per swing, then rests on frame 0 (the ready pose).
    /// Shovel plays once from when the elf started shovelling and holds its last frame.
    /// </summary>
    private float FrameOf(Clip clip, ModelAnimation a, float seconds)
    {
        float last = Math.Max(1, a.KeyFrameCount - 1);
        switch (clip)
        {
            case Clip.Work:
                float f = _swingTime * FramesPerSecond;
                return f >= last ? 0 : f;
            case Clip.Shovel:
                return Math.Min(last, seconds * FramesPerSecond);
            default:
                return seconds * FramesPerSecond % last;
        }
    }
}
