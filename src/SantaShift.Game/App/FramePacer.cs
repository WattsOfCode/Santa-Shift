using System.Diagnostics;

namespace SantaShift.Game.App;

/// <summary>
/// Holds the frame rate cap exactly (Settings > Game > Frame rate). Raylib's own limiter sleeps a whole frame's wait and
/// usually oversleeps, landing at 118-119 instead of 120. This keeps a fixed beat of deadlines: it sleeps while more than
/// a couple of milliseconds remain, then spins for the last moment, so every frame starts on time and the rate doesn't
/// drift. If a frame runs late, the beat restarts from now rather than rushing frames to catch up.
/// </summary>
public sealed class FramePacer
{
    private const double SleepMarginSeconds = 0.002; // below this, spin: a sleep can overshoot by a millisecond or so
    private long _next;

    /// <summary>Waits until the next frame is due at <paramref name="fps"/> frames a second; 0 or less = no wait.</summary>
    public void Wait(int fps)
    {
        if (fps <= 0) { _next = 0; return; }
        long period = Stopwatch.Frequency / fps;
        long now = Stopwatch.GetTimestamp();
        if (_next == 0 || now - _next > period) _next = now; // first frame, or fell behind: start a new beat
        _next += period;

        long sleepMargin = (long)(SleepMarginSeconds * Stopwatch.Frequency);
        while (_next - Stopwatch.GetTimestamp() > sleepMargin) Thread.Sleep(1);
        var spin = new SpinWait();
        while (Stopwatch.GetTimestamp() < _next) spin.SpinOnce(sleep1Threshold: -1); // never sleeps: stays on time
    }
}
