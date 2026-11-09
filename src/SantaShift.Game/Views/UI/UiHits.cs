using System.Numerics;

namespace SantaShift.Game.Views.UI;

/// <summary>
/// The clickable areas a view drew this frame, in logical 960 x 540 coordinates. The view adds each button as it draws
/// it, so what can be clicked always matches what is on screen; the controller asks which one is under the pointer.
/// </summary>
public sealed class UiHits
{
    private readonly List<(int Id, float X, float Y, float W, float H)> _areas = [];

    public void Clear() => _areas.Clear();

    public void Add(int id, float x, float y, float w, float h) => _areas.Add((id, x, y, w, h));

    /// <summary>The id of the area under <paramref name="p"/> (the one added last wins where they overlap), or -1.</summary>
    public int At(Vector2 p)
    {
        for (int i = _areas.Count - 1; i >= 0; i--)
        {
            var a = _areas[i];
            if (p.X >= a.X && p.X <= a.X + a.W && p.Y >= a.Y && p.Y <= a.Y + a.H) return a.Id;
        }
        return -1;
    }

    /// <summary>Where area <paramref name="id"/> was drawn, if it was.</summary>
    public bool TryGet(int id, out (float X, float Y, float W, float H) area)
    {
        foreach (var a in _areas)
            if (a.Id == id) { area = (a.X, a.Y, a.W, a.H); return true; }
        area = default;
        return false;
    }
}
