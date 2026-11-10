using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>
/// The toy cart (see <see cref="ToyCart"/>). At the parked cart, use does one thing depending on the elf's hands:
/// a finished toy goes in (up to the cart's capacity); empty hands open the cart, where part key n takes toy n into the
/// elf's hands and use again grabs the handle (an empty cart is grabbed at once). While pushing, the elf walks at
/// <see cref="CartTuning.SpeedMultiplier"/> times the usual speed and use lets go, leaving the cart where it is.
/// </summary>
public sealed class CartSystem(GameContent content)
{
    private readonly Tuning _t = content.Tuning;

    /// <summary>A built cart for a shift, standing at its home.</summary>
    public static ToyCart NewCart(Tuning t, int parts) => new()
    {
        Parts = Math.Clamp(parts, 0, t.Cart.Parts.Length),
        Built = t.Cart.BuiltWith(parts),
        Position = t.Cart.Home,
    };

    /// <summary>The distance from the elf to the parked, built cart, or null when it can't be used from here.</summary>
    public double? Reach(RunState state)
    {
        var cart = state.Cart;
        if (!cart.Built || cart.Pushing) return null;
        double d = cart.Position.DistanceTo(state.PlayerPosition);
        return d <= _t.InteractRange ? d : null;
    }

    /// <summary>
    /// Handles a press of use that concerns the cart: letting go while pushing, grabbing the handle while the cart is
    /// open, or using the cart when it is the nearest thing in reach (<paramref name="cartSelected"/>). Returns false when
    /// the press is for something else.
    /// </summary>
    public bool TryInteract(RunState state, bool cartSelected, List<SimEvent> output)
    {
        var cart = state.Cart;
        var d = content.Dialogue;
        if (cart.Pushing)
        {
            cart.Pushing = false;
            output.Add(new SimEvent(SimEventType.CartParked));
            return true;
        }
        if (cart.PickOpen) { Grab(state, output); return true; }
        if (!cartSelected) return false;

        var item = state.CarriedItem;
        if (item != null)
        {
            if (item.Stage != ItemStage.Assembled) output.Add(Reject(d.Message("cart_toys_only")));
            else if (cart.Items.Count >= _t.Cart.Capacity) output.Add(Reject(d.Message("cart_full", _t.Cart.Capacity)));
            else
            {
                cart.Items.Add(item);
                state.CarriedItem = null;
                output.Add(new SimEvent(SimEventType.CartLoaded, d.Message("cart_loaded", ToyName(item), cart.Items.Count, _t.Cart.Capacity),
                    ToyId: item.ToyId));
            }
            return true;
        }

        if (cart.Items.Count == 0) { Grab(state, output); return true; }
        cart.PickOpen = true;
        output.Add(new SimEvent(SimEventType.CartPickOpened));
        return true;
    }

    /// <summary>
    /// Every step: the open cart takes part key n as "take toy n" (moving or cancelling closes it), and a pushed cart
    /// swings round towards where the elf walks and rolls ahead of them.
    /// </summary>
    public void Update(RunState state, double moveX, double moveY, bool cancel, int partKey, List<SimEvent> output)
    {
        var cart = state.Cart;
        if (cart.PickOpen)
        {
            bool moving = moveX != 0 || moveY != 0;
            if (moving || cancel || state.CarriedItem != null) cart.PickOpen = false;
            else if (partKey >= 1 && partKey <= cart.Items.Count)
            {
                var item = cart.Items[partKey - 1];
                cart.Items.RemoveAt(partKey - 1);
                cart.PickOpen = false;
                state.CarriedItem = item;
                output.Add(new SimEvent(SimEventType.CartTaken, content.Dialogue.Message("cart_taken", ToyName(item)), ToyId: item.ToyId));
            }
        }

        if (!cart.Pushing) return;
        double length = Math.Sqrt(moveX * moveX + moveY * moveY);
        if (length > 0)
        {
            // Turn by a share of the angle left (by angle, so even walking straight back the cart swings round).
            double current = Math.Atan2(cart.Direction.X, cart.Direction.Y), wanted = Math.Atan2(moveX, moveY);
            double diff = Math.IEEERemainder(wanted - current, 2 * Math.PI);
            double a = current + diff * Math.Clamp(_t.Cart.TurnRate, 0, 1);
            cart.Direction = (Math.Sin(a), Math.Cos(a));
        }
        cart.Position = Ahead(state);
    }

    /// <summary>Puts the elf's hands on the handle: the cart turns to point away from the elf and stays where it is.</summary>
    private void Grab(RunState state, List<SimEvent> output)
    {
        var cart = state.Cart;
        cart.PickOpen = false;
        double dx = cart.Position.X - state.PlayerPosition.X, dy = cart.Position.Y - state.PlayerPosition.Y;
        double n = Math.Sqrt(dx * dx + dy * dy);
        if (n > 1e-6) cart.Direction = (dx / n, dy / n);
        cart.Pushing = true;
        cart.Position = Ahead(state);
        output.Add(new SimEvent(SimEventType.CartGrabbed));
    }

    private Point2 Ahead(RunState state) => new(
        state.PlayerPosition.X + state.Cart.Direction.X * _t.Cart.PushDistance,
        state.PlayerPosition.Y + state.Cart.Direction.Y * _t.Cart.PushDistance);

    private string ToyName(Item item) => content.FindToy(item.ToyId)?.NameOf(item.Variant) ?? item.ToyId;

    private static SimEvent Reject(string text) => new(SimEventType.Rejected, text);
}
