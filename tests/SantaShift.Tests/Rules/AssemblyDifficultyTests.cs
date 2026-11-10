using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>
/// The bench minigame: every toy has fixed build combinations (the plain toy and its variants), each part on the same
/// key every build. It gets harder by round: shorter time, letters asking for variants, then building from memory.
/// </summary>
public class AssemblyDifficultyTests
{
    private static readonly AssemblyTuning A = new Tuning().Assembly;

    private static Simulation NewSim(int round = 1, GameMode mode = GameMode.Standard) =>
        new(TestContent.Create(variants: true), mode, round);

    private static AssemblyState StartBuild(Simulation sim, string toy)
    {
        sim.Use("kits_" + toy);
        sim.Use("bench");
        return sim.State.Assembly!;
    }

    [Fact]
    public void The_time_limit_shrinks_each_round_down_to_the_minimum()
    {
        Assert.Equal(6.0, A.TimeLimitFor(GameMode.Standard, 1));
        Assert.Equal(5.5, A.TimeLimitFor(GameMode.Standard, 2));
        Assert.Equal(A.MinTimeLimitSeconds, A.TimeLimitFor(GameMode.Standard, 50));
    }

    [Fact]
    public void Relaxed_mode_lags_a_round_behind_and_gets_more_time()
    {
        Assert.Equal(9.0, A.TimeLimitFor(GameMode.Relaxed, 1));
        Assert.Equal(9.0, A.TimeLimitFor(GameMode.Relaxed, 2));
        Assert.False(A.Variants(GameMode.Relaxed, 2));
        Assert.True(A.Variants(GameMode.Relaxed, 3));
        Assert.True(A.ShowRecipes(GameMode.Relaxed, 5));
        Assert.False(A.ShowRecipes(GameMode.Relaxed, 6));
    }

    [Fact]
    public void The_plain_train_is_always_one_two_three()
    {
        foreach (int round in new[] { 1, 3, 8 })
        {
            var sim = NewSim(round);
            var game = StartBuild(sim, "train");
            Assert.Equal(["wheels", "boiler", "cab"], game.Keys.Take(3));
            sim.PressPart(1);
            sim.PressPart(2);
            sim.PressPart(3);
            Assert.Equal(ItemStage.Assembled, sim.State.CarriedItem!.Stage);
            Assert.Null(sim.State.CarriedItem.Variant);
        }
    }

    [Fact]
    public void Each_toy_has_its_own_fixed_combination()
    {
        var sim = NewSim();
        StartBuild(sim, "bear");
        sim.PressPart(2); // body
        sim.PressPart(3); // arms
        var events = sim.PressPart(1); // head
        Assert.Contains(events, e => e.Type == SimEventType.AssemblyComplete);

        var robot = NewSim();
        StartBuild(robot, "robot");
        foreach (int key in new[] { 2, 1, 3 }) robot.PressPart(key);
        Assert.Equal(ItemStage.Assembled, robot.State.CarriedItem!.Stage);
    }

    [Fact]
    public void A_key_that_fits_no_combination_botches_the_toy()
    {
        var sim = NewSim();
        StartBuild(sim, "bear");
        var events = sim.PressPart(1); // the head can't go on first
        Assert.Contains(events, e => e.Type == SimEventType.AssemblyBotched);
        Assert.Equal(ItemStage.Botched, sim.State.CarriedItem!.Stage);
    }

    [Fact]
    public void Before_variants_start_their_pieces_are_not_on_the_bench_and_their_key_does_nothing()
    {
        var sim = NewSim(round: 1);
        var game = StartBuild(sim, "train");
        Assert.Equal(3, game.Keys.Count);
        Assert.Single(game.Recipes);

        var events = sim.PressPart(4);
        Assert.DoesNotContain(events, e => e.Type is SimEventType.AssemblyBotched or SimEventType.PartAdded);
        Assert.Same(game, sim.State.Assembly);
    }

    [Fact]
    public void From_round_two_a_variant_is_built_with_its_own_combination()
    {
        var sim = NewSim(round: 2);
        var game = StartBuild(sim, "train");
        Assert.Equal(["wheels", "boiler", "cab", "big_wheels"], game.Keys);
        Assert.Equal(2, game.Recipes.Count);

        sim.PressPart(4); // big wheels: now only the big-wheel train fits
        Assert.Equal("big_wheels", game.Target!.Id);
        sim.PressPart(2);
        var events = sim.PressPart(3);

        Assert.Contains(events, e => e.Type == SimEventType.AssemblyComplete && e.Text == "Big-Wheel Train");
        Assert.Equal("big_wheels", sim.State.CarriedItem!.Variant);
    }

    [Fact]
    public void The_big_bear_takes_the_stuffing_second()
    {
        var sim = NewSim(round: 2);
        StartBuild(sim, "bear");
        foreach (int key in new[] { 2, 4, 3, 1 }) sim.PressPart(key);
        Assert.Equal("big", sim.State.CarriedItem!.Variant);
    }

    [Fact]
    public void From_round_four_a_part_can_go_on_twice()
    {
        Assert.Equal(2, StartBuild(NewSim(round: 3), "bear").Recipes.Count); // the giant bear waits for round 4

        var bear = NewSim(round: 4);
        StartBuild(bear, "bear");
        foreach (int key in new[] { 2, 4, 4, 3, 1 }) bear.PressPart(key);
        Assert.Equal("giant", bear.State.CarriedItem!.Variant);

        var train = NewSim(round: 4);
        var game = StartBuild(train, "train");
        train.PressPart(1);
        train.PressPart(1); // the second set of wheels: now only the long train fits
        Assert.Equal("long", game.Target!.Id);
        train.PressPart(2);
        train.PressPart(3);
        Assert.Equal("long", train.State.CarriedItem!.Variant);
    }

    [Fact]
    public void A_variant_can_start_in_a_later_round_and_its_longer_combination_gets_more_time()
    {
        Assert.Single(StartBuild(NewSim(round: 2), "robot").Recipes); // the rocket pack waits for round 3

        var sim = NewSim(round: 3);
        var game = StartBuild(sim, "robot");
        Assert.Equal(2, game.Recipes.Count);
        Assert.Equal(sim.Tuning.Ticks(A.TimeLimitFor(GameMode.Standard, 3) * 4 / 3), game.DeadlineTick - game.StartTick);

        foreach (int key in new[] { 2, 1, 4, 3 }) sim.PressPart(key);
        Assert.Equal("rocket", sim.State.CarriedItem!.Variant);
    }

    [Fact]
    public void The_bench_suggests_the_variant_the_most_urgent_letter_wants()
    {
        var sim = NewSim(round: 2);
        sim.State.LiveOrders.Clear();
        // Due before any letter the shift sends (those have about a minute), so it is the most urgent.
        sim.State.LiveOrders.Add(new Order { Id = 900, ToyId = "train", Variant = "big_wheels", DeadlineTick = 600 });
        sim.State.LiveOrders.Add(new Order { Id = 901, ToyId = "train", DeadlineTick = 9000 });

        var game = StartBuild(sim, "train");
        Assert.Equal("big_wheels", game.Wanted);
        Assert.Equal(4, game.NextKey);

        sim.FinishCrafting();
        Assert.Equal("big_wheels", sim.State.CarriedItem!.Variant);
    }

    [Fact]
    public void Only_the_variant_asked_for_fills_the_letter()
    {
        var sim = NewSim(round: 2);
        sim.State.SpawnTicks = []; // no other letters this shift
        sim.State.LiveOrders.Clear();
        sim.State.LiveOrders.Add(new Order
        {
            Id = 900, ToyId = "train", Variant = "big_wheels", DeadlineTick = 99999, AllowedDurationTicks = 99999,
            Letter = new ChildLetter { Child = "Sam", ToyId = "train" },
        });

        sim.State.CarriedItem = new Item { ToyId = "train", Stage = ItemStage.Assembled };
        var events = sim.Use("shipping");
        Assert.Contains(events, e => e.Type == SimEventType.Rejected && e.Text!.Contains("wrong_variant"));
        Assert.NotNull(sim.State.CarriedItem);

        sim.State.CarriedItem.Variant = "big_wheels";
        Assert.Contains(sim.Use("shipping"), e => e.Type == SimEventType.Delivered && e.OrderId == 900);
    }

    [Fact]
    public void Letters_ask_for_variants_from_round_two_and_more_often_later()
    {
        int Variants(int round) => NewSim(round).State.OrderPlan.Count(p => p.Variant != null);

        Assert.Equal(0, Variants(1));
        Assert.InRange(Variants(2), 1, 15);
        Assert.True(Variants(4) > Variants(2));
        Assert.All(NewSim(2).State.OrderPlan.Where(p => p.Variant != null), p => Assert.NotEqual("rocket", p.Variant));
    }

    [Fact]
    public void A_naughty_child_never_asks_for_a_variant_and_the_lesson_has_none()
    {
        var everyone = new[] { "A", "B", "C" };
        var letters = everyone.Select(c => new ChildLetter { Id = c, Child = c, Age = 7, ToyId = "train" }).ToList();
        var sim = new Simulation(TestContent.Create(letters, variants: true), GameMode.Standard, 3, naughty: everyone);
        var trains = sim.State.OrderPlan.Where(p => p.ToyId == "train").ToList(); // every train letter is from a naughty child
        Assert.All(trains, p => Assert.True(p.Naughty));
        Assert.All(trains, p => Assert.Null(p.Variant));

        var lesson = new Simulation(TestContent.Create(variants: true), GameMode.Standard, 2, tutorial: true);
        Assert.All(lesson.State.OrderPlan, p => Assert.Null(p.Variant));
    }

    [Fact]
    public void Variant_letters_go_to_variant_orders()
    {
        var letters = new List<ChildLetter>
        {
            new() { Id = "plain", Child = "Ann", Age = 6, ToyId = "train", Line = "plain" },
            new() { Id = "big", Child = "Ben", Age = 6, ToyId = "train", Variant = "big_wheels", Line = "big" },
        };
        var sim = new Simulation(TestContent.Create(letters, variants: true), GameMode.Standard, 4);
        // A variant with no letter of its own (the long train here) borrows a plain one.
        Assert.All(sim.State.OrderPlan.Where(p => p.ToyId == "train"), p => Assert.Equal(p.Variant == "big_wheels" ? "big" : "plain", p.Letter!.Id));
        Assert.Contains(sim.State.OrderPlan, p => p.Variant == "big_wheels");
    }

    [Fact]
    public void From_round_five_the_bench_stops_listing_the_combinations()
    {
        Assert.True(A.ShowRecipes(GameMode.Standard, 4));
        Assert.False(A.ShowRecipes(GameMode.Standard, 5));
    }

    [Fact]
    public void Shipped_toys_have_models_fixed_keys_and_their_combinations()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        Assert.DoesNotContain(content.Warnings, w => w.Contains("variant"));
        Assert.All(content.Toys, t =>
        {
            Assert.All(t.Parts, p => Assert.False(string.IsNullOrEmpty(p.Model)));
            Assert.False(string.IsNullOrEmpty(t.BotchedModel));
        });
        string Keys(Toy t, ToyVariant r) => string.Join(" ", r.Order.Select(t.KeyOf));
        string Of(string toy, string? variant = null) => Keys(content.Toy(toy), content.Toy(toy).Variant(variant)!);
        Assert.Equal("1 2 3", Of("train"));
        Assert.Equal("4 2 3", Of("train", "big_wheels"));
        Assert.Equal("1 1 2 3", Of("train", "long"));
        Assert.Equal("2 3 1", Of("bear"));
        Assert.Equal("2 4 3 1", Of("bear", "big"));
        Assert.Equal("2 4 4 3 1", Of("bear", "giant"));
        Assert.Equal("2 1 3", Of("robot"));
        Assert.Equal("2 1 4 3", Of("robot", "rocket"));
        Assert.Equal("3 1 2", Of("boat"));
        Assert.Equal(5, content.Toy("boat").FromRound);
        // Every other combination differs from the train's, so each toy is its own thing to learn.
        var all = content.Toys.SelectMany(t => t.Recipes.Select(r => Keys(t, r))).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void The_boat_joins_in_round_five_and_its_bin_is_shut_before()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        var early = new Simulation(content, GameMode.Standard, 4);
        Assert.DoesNotContain(early.State.OrderPlan, p => p.ToyId == "boat");
        var events = early.Use("kits_boat");
        Assert.Contains(events, e => e.Type == SimEventType.Rejected && e.ToyId == "boat");
        Assert.Null(early.State.CarriedItem);

        // A supply crate goes in whatever the round (the kits still wait for round 5).
        early.State.CarriedItem = Item.Supply(1);
        Assert.Contains(early.Use("kits_boat"), e => e.Type == SimEventType.Restocked && e.ToyId == "boat");
        Assert.Null(early.State.CarriedItem);
        Assert.True(early.State.KitsLeft("boat") > 0);
        Assert.Contains(early.Use("kits_boat"), e => e.Type == SimEventType.Rejected && e.ToyId == "boat");

        var later = new Simulation(content, GameMode.Standard, 5);
        Assert.Contains(later.State.OrderPlan, p => p.ToyId == "boat" && !p.Naughty);
        later.Use("kits_boat");
        Assert.Equal("boat", later.State.CarriedItem?.ToyId);
    }

    [Fact]
    public void A_toy_without_parts_is_rejected()
    {
        var ex = Assert.Throws<ContentException>(() => ContentLoader.Build(new Tuning(),
            [new Toy { Id = "train" }], [], [], new DialogueBook(), []));
        Assert.Contains("part", ex.Message);
    }

    [Fact]
    public void A_combination_that_starts_another_is_rejected()
    {
        var toy = new Toy
        {
            Id = "robot",
            Parts = [TestContent.Part("legs", 1), TestContent.Part("torso", 2), TestContent.Part("rocket", 3)],
            Variants = [TestContent.Recipe("standard", "Robot", "legs", "torso"), TestContent.Recipe("rocket", "Rocket", "legs", "torso", "rocket")],
        };
        var ex = Assert.Throws<ContentException>(() => ContentLoader.Build(new Tuning(), [toy], [], [], new DialogueBook(), []));
        Assert.Contains("start of", ex.Message);
    }

    [Fact]
    public void Two_parts_on_one_key_are_rejected()
    {
        var toy = new Toy { Id = "train", Parts = [TestContent.Part("wheels", 1), TestContent.Part("cab", 1)] };
        var ex = Assert.Throws<ContentException>(() => ContentLoader.Build(new Tuning(), [toy], [], [], new DialogueBook(), []));
        Assert.Contains("key 1", ex.Message);
    }
}
