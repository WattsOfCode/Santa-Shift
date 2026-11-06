using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>The bench minigame gets harder by round: shorter time, then a shuffled order, then shuffled keys.</summary>
public class AssemblyDifficultyTests
{
    private static readonly AssemblyTuning A = new Tuning().Assembly;
    private static readonly string[] Natural = ["wheels", "boiler", "cab"];

    private static AssemblyState StartBuild(GameMode mode, int round, int seed = 1)
    {
        var sim = new Simulation(TestContent.Create(), mode, round, seed);
        sim.Use("kits_train");
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
        Assert.False(A.ShuffleOrder(GameMode.Relaxed, 2));
        Assert.True(A.ShuffleOrder(GameMode.Relaxed, 3));
    }

    [Fact]
    public void From_round_two_the_order_is_shuffled_but_the_keys_stay_put()
    {
        var game = StartBuild(GameMode.Standard, 2);
        Assert.NotEqual(Natural, game.Order);
        Assert.Equal(Natural.Order(), game.Order.Order());
        Assert.Equal(Natural, game.Keys);
    }

    [Fact]
    public void From_round_four_the_keys_move_too_and_never_just_read_one_two_three()
    {
        for (int seed = 0; seed < 25; seed++)
        {
            var game = StartBuild(GameMode.Standard, 4, seed);
            Assert.NotEqual(Natural, game.Keys);
            Assert.NotEqual(game.Order, game.Keys);
        }
    }

    [Fact]
    public void Shuffles_differ_between_builds_and_repeat_with_the_same_seed()
    {
        var orders = Enumerable.Range(0, 20).Select(s => string.Join(",", StartBuild(GameMode.Standard, 3, s).Order)).ToHashSet();
        Assert.True(orders.Count > 1, "different seeds should give different orders");
        Assert.Equal(StartBuild(GameMode.Standard, 3, 42).Order, StartBuild(GameMode.Standard, 3, 42).Order);
    }

    [Fact]
    public void A_later_round_deadline_uses_its_shorter_time_limit()
    {
        var game = StartBuild(GameMode.Standard, 3);
        Assert.Equal(new Tuning().Ticks(5.0), game.DeadlineTick - game.StartTick);
    }

    [Fact]
    public void Shipped_toys_each_have_three_parts_with_models_and_a_botched_model()
    {
        var content = ContentLoader.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
        Assert.All(content.Toys, t =>
        {
            Assert.Equal(3, t.Parts.Count);
            Assert.All(t.Parts, p => Assert.False(string.IsNullOrEmpty(p.Model)));
            Assert.False(string.IsNullOrEmpty(t.BotchedModel));
        });
    }

    [Fact]
    public void A_toy_without_parts_is_rejected()
    {
        var ex = Assert.Throws<ContentException>(() => ContentLoader.Build(new Tuning(),
            [new Toy { Id = "train" }], [], [], new DialogueBook(), []));
        Assert.Contains("part", ex.Message);
    }
}
