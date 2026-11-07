using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>The lesson round's kit stock with the real data files (letters, stations with the sled, tuning).</summary>
public class ShippedLessonTests
{
    private static readonly GameContent Shipped = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

    public static IEnumerable<object[]> NaughtyLists() =>
        Shipped.Letters.Select(l => new object[] { l.Child }).Take(8).Append([""]);

    [Theory]
    [MemberData(nameof(NaughtyLists))]
    public void Only_the_last_lesson_toy_starts_short(string naughty)
    {
        var sim = new Simulation(Shipped, GameMode.Standard, naughty: naughty == "" ? [] : [naughty], tutorial: true);
        var plan = sim.State.OrderPlan;
        int lesson = Shipped.Tuning.Tutorial.Orders;
        var last = plan[lesson - 1];
        Assert.False(last.Naughty);
        // Its bin holds just the kits for that toy earlier in the lesson, so it runs dry exactly at the last lesson order.
        Assert.Equal(plan.Take(lesson - 1).Count(p => p.ToyId == last.ToyId && !p.Naughty), sim.State.KitsLeft(last.ToyId));
        Assert.DoesNotContain(plan.Skip(lesson), p => p.ToyId == last.ToyId);
        foreach (var toy in Shipped.Toys.Select(t => t.Id).Where(t => t != last.ToyId))
            Assert.Equal(plan.Count(p => p.ToyId == toy && !p.Naughty), sim.State.KitsLeft(toy));
    }
}
