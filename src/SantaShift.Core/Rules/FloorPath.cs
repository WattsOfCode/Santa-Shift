using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>
/// Finds a walkable route across the workshop floor (for the guide arrows): A* on a coarse grid that keeps the elf's
/// width clear of every station and obstacle, then straightened so the arrows run in long, clean lines.
/// </summary>
public sealed class FloorPath
{
    private readonly GameContent _content;
    private readonly double _cell;
    private readonly int _cols, _rows;
    private readonly bool[] _blocked;
    private readonly double _left, _top;

    /// <param name="cellPixels">Grid cell size in logical pixels (16 = 0.4 m).</param>
    public FloorPath(GameContent content, double cellPixels = 16)
    {
        _content = content;
        _cell = cellPixels;
        var f = content.Floor;
        _left = f.Left;
        _top = f.Top;
        _cols = Math.Max(1, (int)Math.Ceiling((f.Right - f.Left) / _cell));
        _rows = Math.Max(1, (int)Math.Ceiling((f.Bottom - f.Top) / _cell));
        _blocked = new bool[_cols * _rows];

        var t = content.Tuning;
        double padX = t.PlayerColliderWidth / 2 + 2, padY = t.PlayerColliderHeight / 2 + 2;
        var boxes = content.Stations.Select(s => s.Body).Concat(content.Colliders).ToList();
        for (int r = 0; r < _rows; r++)
        for (int c = 0; c < _cols; c++)
        {
            var p = Centre(c, r);
            bool hit = p.X < f.Left + padX || p.X > f.Right - padX || p.Y < f.Top + padY || p.Y > f.Bottom - padY;
            foreach (var b in boxes)
                if (!hit && Math.Abs(p.X - b.X) < b.W / 2 + padX && Math.Abs(p.Y - b.Y) < b.H / 2 + padY) hit = true;
            _blocked[r * _cols + c] = hit;
        }
    }

    private Point2 Centre(int c, int r) => new(_left + (c + 0.5) * _cell, _top + (r + 0.5) * _cell);

    private (int C, int R) CellOf(Point2 p) =>
        (Math.Clamp((int)((p.X - _left) / _cell), 0, _cols - 1), Math.Clamp((int)((p.Y - _top) / _cell), 0, _rows - 1));

    public bool IsBlocked(Point2 p)
    {
        var (c, r) = CellOf(p);
        return _blocked[r * _cols + c];
    }

    /// <summary>
    /// A route from <paramref name="from"/> to <paramref name="to"/> as a few corner points (both ends included), or just
    /// the two ends if no route exists. Either end may sit in a blocked cell (the elf at a station); the search starts
    /// and ends at the nearest open cell.
    /// </summary>
    public List<Point2> Find(Point2 from, Point2 to)
    {
        var start = NearestOpen(CellOf(from));
        var goal = NearestOpen(CellOf(to));
        int n = _cols * _rows;
        var cost = new double[n];
        Array.Fill(cost, double.MaxValue);
        var came = new int[n];
        Array.Fill(came, -1);
        var open = new PriorityQueue<int, double>();
        int s = start.R * _cols + start.C, g = goal.R * _cols + goal.C;
        cost[s] = 0;
        open.Enqueue(s, 0);
        while (open.TryDequeue(out int i, out _))
        {
            if (i == g) break;
            int ic = i % _cols, ir = i / _cols;
            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int nc = ic + dc, nr = ir + dr;
                if (nc < 0 || nr < 0 || nc >= _cols || nr >= _rows) continue;
                int j = nr * _cols + nc;
                if (_blocked[j]) continue;
                if (dr != 0 && dc != 0 && (_blocked[ir * _cols + nc] || _blocked[nr * _cols + ic])) continue; // no corner cutting
                double step = dr != 0 && dc != 0 ? 1.4142 : 1;
                if (cost[i] + step >= cost[j]) continue;
                cost[j] = cost[i] + step;
                came[j] = i;
                double h = Math.Sqrt((nc - goal.C) * (nc - goal.C) + (nr - goal.R) * (nr - goal.R));
                open.Enqueue(j, cost[j] + h);
            }
        }
        if (s != g && came[g] < 0) return [from, to];

        var cells = new List<int>();
        for (int i = g; i != -1; i = i == s ? -1 : came[i]) cells.Add(i);
        cells.Reverse();
        var points = new List<Point2> { from };
        points.AddRange(cells.Select(i => Centre(i % _cols, i / _cols)));
        points.Add(to);
        return Straighten(points);
    }

    /// <summary>Drops every corner the elf could cut straight past ("string pulling"), keeping the route clear.</summary>
    private List<Point2> Straighten(List<Point2> points)
    {
        var result = new List<Point2> { points[0] };
        int anchor = 0;
        for (int i = 2; i < points.Count; i++)
        {
            if (Clear(points[anchor], points[i])) continue;
            result.Add(points[i - 1]);
            anchor = i - 1;
        }
        result.Add(points[^1]);
        return result;
    }

    /// <summary>True if the straight line between two points crosses no blocked cell (the ends may be blocked).</summary>
    private bool Clear(Point2 a, Point2 b)
    {
        double d = a.DistanceTo(b);
        int steps = Math.Max(1, (int)(d / (_cell * 0.4)));
        var ca = CellOf(a);
        var cb = CellOf(b);
        for (int k = 1; k < steps; k++)
        {
            double t = (double)k / steps;
            var p = new Point2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            var cell = CellOf(p);
            if (cell == ca || cell == cb) continue;
            if (_blocked[cell.R * _cols + cell.C]) return false;
        }
        return true;
    }

    private (int C, int R) NearestOpen((int C, int R) cell)
    {
        if (!_blocked[cell.R * _cols + cell.C]) return cell;
        for (int radius = 1; radius < Math.Max(_cols, _rows); radius++)
        for (int dr = -radius; dr <= radius; dr++)
        for (int dc = -radius; dc <= radius; dc++)
        {
            int c = cell.C + dc, r = cell.R + dr;
            if (c >= 0 && r >= 0 && c < _cols && r < _rows && !_blocked[r * _cols + c]) return (c, r);
        }
        return cell;
    }
}
