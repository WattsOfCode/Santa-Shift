using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;

namespace SantaShift.Tests.Rules;

/// <summary>
/// The toy cart: built one part per passed round (ready from round 4), it holds up to three finished toys, taken out
/// one at a time, and the elf can push it at three quarters of walking speed.
/// </summary>
public class CartTests
{
    private static readonly CartTuning Cart = new Tuning().Cart;

    private static Simulation NewSim(int cartParts = 3) => new(TestContent.Create(), GameMode.Standard, round: 4, cartParts: cartParts);

    /// <summary>Stands the elf just behind the parked cart (in reach of it, and of nothing else).</summary>
    private static void AtCart(Simulation sim) =>
        sim.PlacePlayer(new Point2(sim.State.Cart.Position.X, sim.State.Cart.Position.Y + 25));

    private static IReadOnlyList<SimEvent> Press(Simulation sim, bool interact = false, int key = 0, double moveX = 0, double moveY = 0)
    {
        sim.DrainEvents();
        sim.Step(new PlayerCommand(moveX, moveY, interact, PartKey: key));
        return sim.DrainEvents();
    }

    private static Item Toy(string id = "train") => new() { ToyId = id, Stage = ItemStage.Assembled };

    [Fact]
    public void Each_passed_round_adds_a_part_so_the_cart_is_ready_for_round_four()
    {
        var run = new RunProgress(GameMode.Standard);
        for (int round = 1; round <= 3; round++)
        {
            Assert.False(Cart.BuiltWith(run.CartParts));
            run.CompleteRound(new RunState { Delivered = 9 }, 9, Cart);
            Assert.Equal(Cart.Parts[round - 1], run.NewCartPart);
            run.StartNextRound();
        }
        Assert.Equal(4, run.Round);
        Assert.True(Cart.BuiltWith(run.CartParts));

        run.CompleteRound(new RunState { Delivered = 9 }, 9, Cart);
        Assert.Null(run.NewCartPart); // already complete
        Assert.Equal(3, run.CartParts);
    }

    [Fact]
    public void A_missed_round_earns_no_part()
    {
        var run = new RunProgress(GameMode.Standard);
        run.CompleteRound(new RunState { Delivered = 2 }, 9, Cart);
        Assert.Equal(0, run.CartParts);
        Assert.Null(run.NewCartPart);
    }

    [Fact]
    public void An_unfinished_cart_stands_at_home_but_cannot_be_used()
    {
        var sim = NewSim(cartParts: 2);
        Assert.Equal(Cart.Home, sim.State.Cart.Position);
        AtCart(sim);
        Assert.False(sim.CartSelected());
        sim.State.CarriedItem = Toy();
        Press(sim, interact: true);
        Assert.Empty(sim.State.Cart.Items);
        Assert.NotNull(sim.State.CarriedItem);
    }

    [Fact]
    public void Finished_toys_go_in_up_to_three_and_nothing_else_does()
    {
        var sim = NewSim();
        AtCart(sim);
        Assert.True(sim.CartSelected());

        for (int i = 0; i < 3; i++)
        {
            sim.State.CarriedItem = Toy(i == 1 ? "bear" : "train");
            Assert.Contains(Press(sim, interact: true), e => e.Type == SimEventType.CartLoaded);
            Assert.Null(sim.State.CarriedItem);
        }
        Assert.Equal(3, sim.State.Cart.Items.Count);

        sim.State.CarriedItem = Toy("robot");
        Assert.Contains(Press(sim, interact: true), e => e.Type == SimEventType.Rejected && e.Text == "cart_full");
        Assert.NotNull(sim.State.CarriedItem);

        var kit = NewSim();
        AtCart(kit);
        kit.State.CarriedItem = new Item { ToyId = "train" }; // a kit
        Assert.Contains(Press(kit, interact: true), e => e.Type == SimEventType.Rejected && e.Text == "cart_toys_only");
        kit.State.CarriedItem = Item.Coal(1);
        Assert.Contains(Press(kit, interact: true), e => e.Type == SimEventType.Rejected && e.Text == "cart_toys_only");
        Assert.Empty(kit.State.Cart.Items);
    }

    [Fact]
    public void With_empty_hands_the_cart_opens_and_a_key_takes_that_toy()
    {
        var sim = NewSim();
        AtCart(sim);
        var train = Toy("train");
        var bear = Toy("bear");
        sim.State.Cart.Items.AddRange([train, bear]);

        Assert.Contains(Press(sim, interact: true), e => e.Type == SimEventType.CartPickOpened);
        Assert.True(sim.State.Cart.PickOpen);

        Assert.Contains(Press(sim, key: 2), e => e.Type == SimEventType.CartTaken);
        Assert.Same(bear, sim.State.CarriedItem);
        Assert.Equal([train], sim.State.Cart.Items);
        Assert.False(sim.State.Cart.PickOpen);
    }

    [Fact]
    public void Moving_closes_the_open_cart_and_a_key_with_no_toy_does_nothing()
    {
        var sim = NewSim();
        AtCart(sim);
        sim.State.Cart.Items.Add(Toy());
        Press(sim, interact: true);

        Press(sim, key: 3);
        Assert.True(sim.State.Cart.PickOpen);
        Assert.Null(sim.State.CarriedItem);

        Press(sim, moveX: 1);
        Assert.False(sim.State.Cart.PickOpen);
        Assert.Single(sim.State.Cart.Items);
    }

    [Fact]
    public void Pushing_the_cart_is_three_quarters_of_walking_speed_and_it_rolls_ahead()
    {
        var walker = NewSim();
        var pusher = NewSim();
        AtCart(walker);
        AtCart(pusher);
        Assert.Contains(Press(pusher, interact: true), e => e.Type == SimEventType.CartGrabbed); // an empty cart is grabbed at once
        Assert.True(pusher.State.Cart.Pushing);

        var (w0, p0) = (walker.State.PlayerPosition, pusher.State.PlayerPosition);
        for (int i = 0; i < 30; i++) { Press(walker, moveY: 1); Press(pusher, moveY: 1); }
        double walked = walker.State.PlayerPosition.Y - w0.Y, pushed = pusher.State.PlayerPosition.Y - p0.Y;
        Assert.Equal(walked * Cart.SpeedMultiplier, pushed, 3);

        // The cart has swung round to point the way the elf walks, and rolls that far ahead.
        var c = pusher.State.Cart;
        Assert.True(c.Direction.Y > 0.9);
        Assert.Equal(Cart.PushDistance, c.Position.DistanceTo(pusher.State.PlayerPosition), 3);
    }

    [Fact]
    public void While_pushing_use_lets_go_and_the_cart_stays_put_with_its_toys()
    {
        var sim = NewSim();
        AtCart(sim);
        sim.State.Cart.Items.Add(Toy());
        Press(sim, interact: true); // opens it (it has a toy)
        Press(sim, interact: true); // use again grabs the handle
        Assert.True(sim.State.Cart.Pushing);
        Assert.Null(sim.SelectedStation());

        for (int i = 0; i < 60; i++) Press(sim, moveX: -1);
        Assert.Contains(Press(sim, interact: true), e => e.Type == SimEventType.CartParked);
        var parked = sim.State.Cart.Position;

        for (int i = 0; i < 30; i++) Press(sim, moveY: 1);
        Assert.Equal(parked, sim.State.Cart.Position);
        Assert.Single(sim.State.Cart.Items);
    }

    [Fact]
    public void The_first_round_with_the_cart_has_Santa_announce_it()
    {
        var sim = NewSim();
        var events = new List<SimEvent>();
        for (int i = 0; i < sim.Tuning.Ticks(15); i++) { sim.Step(PlayerCommand.None); events.AddRange(sim.DrainEvents()); }
        Assert.Contains(events, e => e.Type == SimEventType.PolicyStarted && e.Text == null); // the cue has no text in tests

        var later = new Simulation(TestContent.Create(), GameMode.Standard, round: 5, cartParts: 3);
        var quiet = new List<SimEvent>();
        for (int i = 0; i < later.Tuning.Ticks(15); i++) { later.Step(PlayerCommand.None); quiet.AddRange(later.DrainEvents()); }
        Assert.True(events.Count(e => e.Type == SimEventType.PolicyStarted) > quiet.Count(e => e.Type == SimEventType.PolicyStarted));
    }
}
