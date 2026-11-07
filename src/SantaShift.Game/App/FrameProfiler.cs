using System.Diagnostics;

namespace SantaShift.Game.App;

/// <summary>
/// Debug builds only (--profile): times named sections of each frame on the CPU and prints the averages every
/// two seconds. Costs nothing when off.
/// </summary>
public static class FrameProfiler
{
    public static bool Enabled;

    private static readonly Dictionary<string, (double Total, int Count)> Sections = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static double _lastReport;
    private static int _frames;
    private static long _allocatedAtReport = GC.GetAllocatedBytesForCurrentThread();
    private static int _gen0AtReport = GC.CollectionCount(0);

    public readonly struct Scope(string name, long start) : IDisposable
    {
        public void Dispose()
        {
            if (name == null) return;
            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            Sections[name] = Sections.TryGetValue(name, out var s) ? (s.Total + ms, s.Count + 1) : (ms, 1);
        }
    }

    public static Scope Time(string name) => Enabled ? new Scope(name, Stopwatch.GetTimestamp()) : default;

    public static void EndFrame()
    {
        if (!Enabled) return;
        _frames++;
        double now = Clock.Elapsed.TotalSeconds;
        if (now - _lastReport < 2) return;
        double span = now - _lastReport;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        int gen0 = GC.CollectionCount(0);
        Console.WriteLine($"[profile] {_frames / span:F0} fps, {span * 1000 / _frames:F2} ms/frame, " +
            $"{(allocated - _allocatedAtReport) / 1024.0 / _frames:F1} KB allocated/frame, {gen0 - _gen0AtReport} gen0 GCs");
        (_allocatedAtReport, _gen0AtReport) = (allocated, gen0);
        foreach (var (name, (total, _)) in Sections.OrderByDescending(s => s.Value.Total))
            Console.WriteLine($"[profile]   {name,-18} {total / _frames:F3} ms");
        Sections.Clear();
        _frames = 0;
        _lastReport = now;
    }
}
