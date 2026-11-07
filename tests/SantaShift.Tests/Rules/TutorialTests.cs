using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>The guided first round: arranged lesson orders, the next-station guide, and the floor route for the arrows.</summary>
public class TutorialTests
{
    private static readonly List<ChildLetter> Letters =
    [
        new() { Id = "ann", Child = "Ann", Age = 6, ToyId = "train", Line = "Choo" },
        new() { Id = "ben", Child = "Ben", Age = 7, ToyId = "bear", Line = "Hug" },
        new() { Id = "cat", Child = "Cat", Age = 8, ToyId = "robot", Line = "Beep" },
        new() { Id = "dan", Child = "Dan", Age = 5, ToyId = "train", Line = "Toot" },
    ];

    private static Simulation NewSim(bool tutorial = true, params string[] naughty) =>
        new(TestContent.Create(Letters, new Tuning { MaxLiveOrders = 9 }), GameMode.Standard, naughty: naughty, tutorial: tutorial); // no deliveries here, so room for all six

    /// <summary>Plays the first six orders' arrival (orders come every 18 s).</summary>
    private static List<Order> FirstSix(Simulation sim)
    {
        sim.WaitSeconds(91);
        return sim.State.LiveOrders.Concat(sim.State.ClosedOrders).OrderBy(o => o.Id).Take(6).ToList();
    }

    [Fact]
    public void The_lesson_has_one_coal_order_and_one_ribbon_order()
    {
        var sim = NewSim(naughty: "Cat");
        var orders = FirstSix(sim);
        Assert.Equal(6, sim.State.TutorialOrderIds.Count);
        Assert.Single(orders, o => o.Naughty);
        Assert.True(orders[sim.Tuning.Tutorial.CoalOrder - 1].Naughty);
        Assert.Single(orders, o => o.RequiresRibbon);
        Assert.True(orders[sim.Tuning.Tutorial.RibbonOrder - 1].RequiresRibbon);
        Assert.DoesNotContain(orders, o => o.IsRush);
    }

    [Fact]
    public void A_naughty_child_does_not_turn_other_lessons_into_coal()
    {
        var sim = NewSim(naughty: "Ann"); // Ann wrote the very first train letter
        var orders = FirstSix(sim);
        Assert.False(orders[0].Naughty);
        Assert.NotEqual("Ann", orders[0].Letter?.Child);
    }

    [Fact]
    public void Without_the_tutorial_orders_follow_the_usual_rules()
    {
        var sim = NewSim(tutorial: false, naughty: "Ann");
        var orders = FirstSix(sim);
        Assert.Empty(sim.State.TutorialOrderIds);
        Assert.True(orders[0].Naughty);
    }

    [Fact]
    public void The_guide_walks_through_a_toy_from_kit_to_chute()
    {
        var sim = NewSim();
        sim.Wait(1);
        Assert.Equal("kits_train", TutorialGuide.Next(sim)!.Station.Id);
        sim.Use("kits_train");
        Assert.Equal("bench", TutorialGuide.Next(sim)!.Station.Id);
        sim.Use("bench");
        Assert.Null(TutorialGuide.Next(sim)); // busy building: no arrows over the work
        sim.FinishCrafting();
        Assert.Equal("shipping", TutorialGuide.Next(sim)!.Station.Id);
        sim.Use("shipping");
        Assert.Equal(1, TutorialGuide.Done(sim.State));
    }

    [Fact]
    public void The_guide_sends_coal_orders_to_the_fireplace_and_ribbon_orders_to_the_ribbon()
    {
        var sim = NewSim(naughty: "Cat");
        sim.State.LiveOrders.Clear();
        sim.State.LiveOrders.Add(new Order { Id = 99, ToyId = "robot", Naughty = true, DeadlineTick = 9999, AllowedDurationTicks = 9999 });
        Assert.Equal("coal", TutorialGuide.Next(sim)!.Station.Id);

        sim.State.LiveOrders.Clear();
        sim.State.LiveOrders.Add(new Order { Id = 98, ToyId = "train", RequiresRibbon = true, DeadlineTick = 9999, AllowedDurationTicks = 9999 });
        sim.State.CarriedItem = new Item { ToyId = "train", Stage = ItemStage.Assembled };
        Assert.Equal("ribbon", TutorialGuide.Next(sim)!.Station.Id);
        sim.State.CarriedItem.HasRibbon = true;
        Assert.Equal("shipping", TutorialGuide.Next(sim)!.Station.Id);
    }

    [Fact]
    public void A_botched_toy_goes_to_the_scrap_barrel()
    {
        var sim = NewSim();
        sim.State.CarriedItem = new Item { ToyId = "train", Stage = ItemStage.Botched };
        sim.Wait(1);
        Assert.Equal("recycle", TutorialGuide.Next(sim)!.Station.Id);
    }

    [Fact]
    public void The_guide_ends_after_the_lesson_orders()
    {
        var sim = NewSim();
        Assert.True(TutorialGuide.Active(sim));
        foreach (var id in Enumerable.Range(1, 6)) { sim.State.TutorialOrderIds.Add(id); sim.State.ClosedOrders.Add(new Order { Id = id, ToyId = "train" }); }
        Assert.False(TutorialGuide.Active(sim));
        Assert.Null(TutorialGuide.Next(sim));
        Assert.False(TutorialGuide.Active(NewSim(tutorial: false)));
    }

    private static Simulation LessonWithSled(bool tutorial = true) =>
        new(TestContent.Create(Letters, new Tuning { MaxLiveOrders = 9 }, supply: true), GameMode.Standard,
            naughty: ["Cat"], tutorial: tutorial);

    [Fact]
    public void The_lesson_round_is_one_kit_short_for_the_last_lesson_order()
    {
        var sim = LessonWithSled();
        var plan = sim.State.OrderPlan;
        string last = plan[sim.Tuning.Tutorial.Orders - 1].ToyId;
        Assert.Equal("robot", last);
        Assert.False(plan[sim.Tuning.Tutorial.Orders - 1].Naughty);
        Assert.Equal(0, sim.State.KitsLeft(last)); // its bin starts empty
        Assert.DoesNotContain(plan.Skip(sim.Tuning.Tutorial.Orders), p => p.ToyId == last); // and nobody else asks for one
        // Every other toy has a kit for every nice child who will ask for it.
        foreach (var toy in new[] { "train", "bear" })
            Assert.Equal(plan.Count(p => p.ToyId == toy && !p.Naughty), sim.State.KitsLeft(toy));
    }

    [Fact]
    public void The_guide_teaches_the_supply_sled_on_the_last_lesson_order()
    {
        var sim = LessonWithSled();
        sim.WaitSeconds(91); // all six lesson orders are in
        var sixth = sim.State.LiveOrders.Single(o => o.Id == sim.State.TutorialOrderIds.Max());
        Assert.Equal("robot", sixth.ToyId);
        sim.State.LiveOrders.RemoveAll(o => o != sixth); // say the first five are done

        Assert.Equal("supply", TutorialGuide.Next(sim)!.Station.Id);
        sim.Use("supply");
        Assert.Equal("kits_robot", TutorialGuide.Next(sim)!.Station.Id); // restock that bin
        sim.Use("kits_robot");
        Assert.Equal(sim.Tuning.ResupplyKits, sim.State.KitsLeft("robot"));
        Assert.Equal("kits_robot", TutorialGuide.Next(sim)!.Station.Id); // now collect from it
    }

    [Fact]
    public void Without_the_lesson_no_bin_starts_short()
    {
        var sim = LessonWithSled(tutorial: false);
        foreach (var toy in new[] { "train", "bear", "robot" })
            Assert.Equal(sim.State.OrderPlan.Count(p => p.ToyId == toy && !p.Naughty), sim.State.KitsLeft(toy));
        Assert.Contains(sim.State.OrderPlan, p => p.ToyId == "robot"); // robots stay in the rotation
    }

    [Fact]
    public void The_floor_route_goes_around_stations()
    {
        var content = TestContent.Create(Letters);
        var path = new FloorPath(content);
        var bench = content.Stations.Single(s => s.Id == "bench"); // body at (300, 255), 120 x 40
        var from = new Point2(300, 210);
        var to = new Point2(300, 300);
        var route = path.Find(from, to);

        Assert.True(route.Count >= 3, "a straight line would cross the bench");
        for (int i = 0; i + 1 < route.Count; i++)
            for (double t = 0.05; t < 1; t += 0.05)
            {
                var p = new Point2(route[i].X + (route[i + 1].X - route[i].X) * t, route[i].Y + (route[i + 1].Y - route[i].Y) * t);
                Assert.False(Math.Abs(p.X - bench.Body.X) < bench.Body.W / 2 && Math.Abs(p.Y - bench.Body.Y) < bench.Body.H / 2,
                    $"route passes through the bench at {p}");
            }
    }

    [Fact]
    public void An_open_floor_route_is_a_straight_line()
    {
        var content = TestContent.Create(Letters);
        var route = new FloorPath(content).Find(new Point2(450, 380), new Point2(650, 380));
        Assert.Equal(2, route.Count);
    }
}
