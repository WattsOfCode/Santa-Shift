namespace SantaShift.Core.Models;

/// <summary>A point on the workshop floor, in GDD logical pixels (960 x 540 canvas).</summary>
public readonly record struct Point2(double X, double Y)
{
    public double DistanceTo(Point2 other)
    {
        double dx = X - other.X, dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>An axis-aligned box on the floor. X/Y are the centre; W/H are the full size.</summary>
public readonly record struct Box2(double X, double Y, double W, double H)
{
    public double Left => X - W / 2;
    public double Right => X + W / 2;
    public double Top => Y - H / 2;
    public double Bottom => Y + H / 2;

    public bool Overlaps(Box2 other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}
