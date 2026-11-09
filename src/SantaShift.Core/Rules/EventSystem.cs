using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>Fires Santa's scheduled warnings and policy changes (GDD section 10).</summary>
public sealed class EventSystem(GameContent content)
{
    private readonly Tuning _t = content.Tuning;

    public void Update(RunState state, List<SimEvent> output)
    {
        long tick = state.Tick;
        var fired = state.Events.FiredEventIds;

        foreach (var e in content.Events)
        {
            if (tick >= _t.Ticks(e.WarnAt) && fired.Add(e.Id + ":warn"))
                output.Add(CueEvent(SimEventType.PolicyWarning, e.WarningCue));

            if (tick >= _t.Ticks(e.StartAt) && fired.Add(e.Id + ":start"))
            {
                if (e.Type == SantaEventType.RibbonWindow)
                    state.Events.RibbonWindowEndTick = _t.Ticks(e.EndAt ?? e.StartAt);
                else if (e.EndAt is { } end)
                    state.Events.RushWindowEndTick = _t.Ticks(end); // a stretch of rush orders
                else
                    state.Events.PendingRush = true; // consumed only when an order is actually created

                if (e.StartCue != null) output.Add(CueEvent(SimEventType.PolicyStarted, e.StartCue));
            }
        }

        // Notices at the start of a shift, a moment after the first letters (so a naughty letter doesn't hide them).
        if (state.RushShift && tick >= _t.Ticks(1) && fired.Add("rush_shift"))
            output.Add(CueEvent(SimEventType.PolicyStarted, "rush_shift"));
        if (tick >= _t.Ticks(state.RushShift ? 7 : 1) && fired.Add("assembly_policy")
            && _t.Assembly.PolicyChange(state.Mode, state.Round) is { } cue)
            output.Add(CueEvent(SimEventType.PolicyStarted, cue));
    }

    private SimEvent CueEvent(SimEventType type, string? cueId)
    {
        var cue = content.Dialogue.Cue(cueId);
        return new SimEvent(type, Text: cue?.Text, SantaLine: cue?.Santa);
    }
}
