using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>The delivery score formula from GDD section 12.</summary>
public static class Scoring
{
    /// <param name="streak">The streak count including this delivery (first delivery = 1).</param>
    public static int Points(Order order, long deliveryTick, int streak, ScoreTuning s)
    {
        int points = s.Base;
        if (order.RequiresRibbon) points += s.RibbonBonus;
        if (order.IsRush) points += s.RushBonus;
        points += TimeBonus(order, deliveryTick, s);
        points += s.StreakStep * Math.Clamp(streak - 1, 0, s.StreakCap);
        return points;
    }

    /// <summary>floor(max × remaining / allowed), clamped to 0..max. Uses whole ticks, so there is no floating-point drift.</summary>
    public static int TimeBonus(Order order, long deliveryTick, ScoreTuning s)
    {
        if (order.AllowedDurationTicks <= 0) return 0;
        long remaining = order.RemainingTicks(deliveryTick);
        long bonus = s.TimeBonusMax * remaining / order.AllowedDurationTicks;
        return (int)Math.Clamp(bonus, 0, s.TimeBonusMax);
    }

    /// <summary>The review label key in dialogue.json, based only on delivered count.</summary>
    public static string ReviewKey(int delivered, Tuning t) =>
        delivered >= t.LegendDelivered ? "review_legend"
        : delivered >= t.SurvivorDelivered ? "review_survivor"
        : "review_low";
}
