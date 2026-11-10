using SantaShift.Core.Content;
using SantaShift.Core.Models;
using SantaShift.Core.Rules;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Controllers;

/// <summary>
/// Reads the interact key, and turns simulation events into on-screen messages and sounds.
/// It only reports what happened; score and state are changed by the rules alone.
/// </summary>
public sealed class InteractionController(GameContent content, IInputService input, IAudioService audio)
{
    public Feedback Feedback { get; } = new();

    public bool ReadInteract() => input.ConsumePressed(InputAction.Interact);

    public void Handle(IReadOnlyList<SimEvent> events)
    {
        foreach (var e in events)
        {
            switch (e.Type)
            {
                case SimEventType.Collected:
                case SimEventType.CoalCollected:
                    audio.Play(SoundIds.KitPickup);
                    break;
                case SimEventType.SupplyTaken:
                    audio.Play(SoundIds.CratePickup);
                    break;
                case SimEventType.ShovelStarted:
                    audio.Play(SoundIds.CoalShovel);
                    break;
                case SimEventType.RibbonStarted:
                    audio.Play(SoundIds.RibbonPick);
                    break;
                case SimEventType.CartLoaded:
                case SimEventType.CartTaken:
                    audio.Play(SoundIds.KitPickup, pitch: e.Type == SimEventType.CartLoaded ? 0.9f : 1.1f);
                    if (e.Text != null) Feedback.ShowToast(e.Text, ToastKind.Info, 70);
                    break;
                case SimEventType.CartGrabbed:
                case SimEventType.CartParked:
                    audio.Play(SoundIds.CratePickup, pitch: e.Type == SimEventType.CartGrabbed ? 1f : 0.85f);
                    break;
                case SimEventType.CartPickOpened:
                    audio.Play(SoundIds.UiMove);
                    break;
                case SimEventType.Restocked:
                    audio.Play(SoundIds.BinRestock);
                    if (e.Text != null) Feedback.ShowToast(e.Text, ToastKind.Success, 90);
                    break;
                case SimEventType.PartAdded:
                    audio.Play(SoundIds.HammerHit, pitch: 1f + 0.07f * Math.Max(0, e.Points - 1)); // rises per part
                    break;
                case SimEventType.AssemblyComplete:
                    audio.Play(SoundIds.AssemblyComplete);
                    break;
                case SimEventType.AssemblyBotched:
                    audio.Play(SoundIds.AssemblyBotched);
                    if (e.Text != null) Feedback.ShowToast(e.Text, ToastKind.Rejected, 120);
                    break;
                case SimEventType.RibbonComplete:
                    audio.Play(SoundIds.RibbonComplete);
                    break;
                case SimEventType.Delivered:
                    audio.Play(e.ToyId == Item.CoalId ? SoundIds.CoalDelivered : SoundIds.DeliverySuccess);
                    if (e.Text != null) Feedback.ShowToast(e.Text, ToastKind.Success);
                    break;
                case SimEventType.Rejected:
                case SimEventType.ActionCancelled:
                    audio.Play(SoundIds.ActionRejected);
                    if (e.Text != null) Feedback.ShowToast(e.Text, ToastKind.Rejected, 90);
                    break;
                case SimEventType.PunchPrompt:
                    audio.Play(SoundIds.PunchClock);
                    Feedback.ShowToast(content.Dialogue.Message("punch_prompt"), ToastKind.Info, 120);
                    break;
                case SimEventType.Recycled:
                    audio.Play(SoundIds.Recycle);
                    if (e.Text != null) Feedback.ShowToast(e.Text, ToastKind.Info, 60);
                    break;
                case SimEventType.OrderExpired:
                    audio.Play(SoundIds.OrderExpired);
                    if (e.Text != null) Feedback.ShowToast(e.Text, ToastKind.Rejected, 90);
                    break;
                case SimEventType.OrderCreated:
                    audio.Play(SoundIds.OrderNew);
                    ShowLetterFor(e.OrderId);
                    break;
                case SimEventType.PolicyWarning:
                case SimEventType.PolicyStarted:
                case SimEventType.QuotaMet:
                    audio.Play(SoundIds.PolicyChime);
                    Feedback.ShowSanta(e.SantaLine, e.Text);
                    break;
                case SimEventType.ShiftEnded:
                    audio.Play(SoundIds.ShiftEnd);
                    break;
            }
        }
    }

    public void Tick() => Feedback.Tick();

    private Simulation? _sim;

    /// <summary>Gives access to live orders so new letters can be shown.</summary>
    public void Attach(Simulation sim) => _sim = sim;

    private void ShowLetterFor(int orderId)
    {
        var order = _sim?.State.LiveOrders.FirstOrDefault(o => o.Id == orderId);
        if (order?.Letter is not { } letter) return;
        string toy = content.FindToy(order.ToyId)?.NameOf(order.Variant) ?? order.ToyId;
        if (content.Tuning.FindRibbon(order.RibbonColor) is { } ribbon) toy += $", {ribbon.Name.ToLowerInvariant()} ribbon";
        Feedback.ShowLetter($"New letter from {letter.Child}, {letter.Age} ({toy}): \"{letter.Line}\"");
        if (order.Naughty)
        {
            audio.Play(SoundIds.PolicyChime);
            Feedback.ShowSanta(content.Dialogue.Cue("naughty_order")?.Santa, content.Dialogue.Message("naughty_order", letter.Child));
        }
    }
}
