using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>Moves the elf with simple box collisions. No physics engine, no pathfinding (GDD section 13).</summary>
public sealed class PlayerMotion(GameContent content)
{
    private readonly Tuning _t = content.Tuning;

    /// <summary>Moves one step. Each axis is resolved separately, so the elf slides along walls.</summary>
    public void Move(RunState state, double moveX, double moveY)
    {
        double length = Math.Sqrt(moveX * moveX + moveY * moveY);
        if (length == 0) return;
        if (length > 1) { moveX /= length; moveY /= length; } // same speed in every direction

        double step = _t.PlayerSpeed / _t.TicksPerSecond;
        var p = state.PlayerPosition;

        var tryX = p with { X = p.X + moveX * step };
        if (!Blocked(tryX)) p = tryX;

        var tryY = p with { Y = p.Y + moveY * step };
        if (!Blocked(tryY)) p = tryY;

        state.PlayerPosition = p;
    }

    public Box2 ColliderAt(Point2 p) => new(p.X, p.Y, _t.PlayerColliderWidth, _t.PlayerColliderHeight);

    private bool Blocked(Point2 p)
    {
        var box = ColliderAt(p);
        var f = content.Floor;
        if (box.Left < f.Left || box.Right > f.Right || box.Top < f.Top || box.Bottom > f.Bottom) return true;
        foreach (var c in content.Colliders)
            if (box.Overlaps(c)) return true;
        foreach (var s in content.Stations)
            if (box.Overlaps(s.Body)) return true;
        return false;
    }
}
