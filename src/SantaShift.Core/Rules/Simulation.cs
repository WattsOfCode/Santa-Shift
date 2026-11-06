using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>
/// Runs one shift. Call <see cref="Step"/> 60 times per simulated second.
/// The step order follows GDD section 11: shift end, events, new orders, movement and crafting,
/// interactions (shipping), then expirations.
/// </summary>
public sealed class Simulation
{
    public GameContent Content { get; }
    public RunState State { get; }

    private readonly PlayerMotion _motion;
    private readonly CraftingSystem _crafting;
    private readonly OrderSystem _orders;
    private readonly EventSystem _events;
    private readonly List<SimEvent> _pending = new();

    /// <param name="seed">Seeds the shuffled part orders. Leave it out for a repeatable shift (tests); the game passes a fresh one.</param>
    public Simulation(GameContent content, GameMode mode, int round = 1, int? seed = null)
    {
        Content = content;
        State = new RunState { Mode = mode, Round = Math.Max(1, round), PlayerPosition = content.PlayerStart };
        _motion = new PlayerMotion(content);
        _crafting = new CraftingSystem(content, new Random(seed ?? State.Round * 7919 + (int)mode));
        _orders = new OrderSystem(content);
        _events = new EventSystem(content);
    }

    public Tuning Tuning => Content.Tuning;
    public long RemainingShiftTicks => Math.Max(0, Tuning.ShiftTicks - State.Tick);
    public int Quota => Tuning.QuotaFor(State.Mode, State.Round);

    public void Step(PlayerCommand cmd)
    {
        if (State.Phase != RunPhase.Playing) return;

        if (State.Tick >= Tuning.ShiftTicks) { EndShift(); return; }

        _events.Update(State, _pending);
        _orders.SpawnScheduled(State, _pending);

        _motion.Move(State, cmd.MoveX, cmd.MoveY);
        _crafting.Update(State, cmd.IsMoving, cmd.PartKey, _pending);
        if (cmd.IsMoving || cmd.Cancel) State.RecyclePromptOpen = false;

        if (cmd.Interact) Interact();

        _orders.Expire(State, _pending);
        State.Tick++;
    }

    /// <summary>Returns and clears everything that happened since the last call.</summary>
    public IReadOnlyList<SimEvent> DrainEvents()
    {
        var copy = _pending.ToArray();
        _pending.Clear();
        return copy;
    }

    /// <summary>Pausing stops the clock and cancels a ribbon (the item is kept). The bench minigame just freezes.</summary>
    public void Pause()
    {
        if (State.Phase != RunPhase.Playing) return;
        CraftingSystem.CancelSilently(State, includeAssembly: false);
        State.RecyclePromptOpen = false;
        State.Phase = RunPhase.Paused;
    }

    public void Resume()
    {
        if (State.Phase == RunPhase.Paused) State.Phase = RunPhase.Playing;
    }

    /// <summary>The nearest station within reach; ties go to the smaller id so selection never flickers.</summary>
    public Station? SelectedStation()
    {
        Station? best = null;
        double bestDistance = double.MaxValue;
        foreach (var s in Content.Stations)
        {
            double distance = s.Interact.DistanceTo(State.PlayerPosition);
            if (distance > Tuning.InteractRange) continue;
            if (distance < bestDistance || (distance == bestDistance && string.CompareOrdinal(s.Id, best!.Id) < 0))
            {
                best = s;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>The prompt for the selected station, naming the actual action ("E  Assemble Train").</summary>
    public string? Prompt()
    {
        var d = Content.Dialogue;
        var item = State.CarriedItem;
        var station = SelectedStation();

        if (State.Assembly != null) return d.Message("working_assemble", ToyName(item));
        if (State.CraftingAction != null) return d.Message("working_ribbon", ToyName(item));
        if (State.RecyclePromptOpen) return d.Message("prompt_recycle_confirm", ItemName(item));
        if (station == null) return null;
        bool botched = item is { Stage: ItemStage.Botched };
        if (botched && station.Type is StationType.Bench or StationType.Ribbon or StationType.Shipping)
            return d.Message("botched_scrap_it");

        return station.Type switch
        {
            StationType.Dispenser => item == null
                ? d.Message("prompt_collect", Content.Toy(station.ToyId!).Name)
                : d.Message("hands_full"),
            StationType.Bench => item is { Stage: ItemStage.Kit }
                ? d.Message("prompt_assemble", ToyName(item))
                : d.Message(item == null ? "bench_needs_kit" : "already_assembled"),
            StationType.Ribbon => item is { Stage: ItemStage.Assembled, HasRibbon: false }
                ? d.Message("prompt_ribbon", ToyName(item))
                : d.Message(item == null ? "ribbon_needs_toy" : item.HasRibbon ? "already_ribboned" : "assemble_first"),
            StationType.Shipping => item is { Stage: ItemStage.Assembled }
                ? d.Message("prompt_ship", ItemName(item))
                : d.Message(item == null ? "nothing_to_ship" : "assemble_first"),
            StationType.Recycle when botched => d.Message("prompt_scrap_botched", ItemName(item)),
            StationType.Recycle => item != null
                ? d.Message("prompt_recycle", ItemName(item))
                : d.Message("nothing_to_recycle"),
            _ => null,
        };
    }

    /// <summary>"Train kit", "Train" or "Train with ribbon".</summary>
    public string ItemName(Item? item)
    {
        if (item == null) return Content.Dialogue.Message("empty_hands");
        string name = ToyName(item);
        if (item.Stage == ItemStage.Kit) return Content.Dialogue.Message("item_kit", name);
        if (item.Stage == ItemStage.Botched) return Content.Dialogue.Message("item_botched", name);
        return item.HasRibbon ? Content.Dialogue.Message("item_ribboned", name) : name;
    }

    /// <summary>"Wheels", for a toy part id.</summary>
    public string PartName(string toyId, string partId) => _crafting.PartName(toyId, partId);

    private string ToyName(Item? item) => item == null ? "" : Content.FindToy(item.ToyId)?.Name ?? item.ToyId;

    private void Interact()
    {
        if (State.IsWorking) return; // already busy; ignore extra presses
        var station = SelectedStation();
        if (station == null) return;
        var d = Content.Dialogue;

        switch (station.Type)
        {
            case StationType.Dispenser:
                if (State.CarriedItem != null) { _pending.Add(new SimEvent(SimEventType.Rejected, d.Message("hands_full"))); break; }
                State.CarriedItem = new Item { ToyId = station.ToyId!, Revision = State.NextId() };
                _pending.Add(new SimEvent(SimEventType.Collected, ToyId: station.ToyId));
                break;

            case StationType.Bench:
                _crafting.TryAssemble(State, station, _pending);
                break;

            case StationType.Ribbon:
                _crafting.TryRibbon(State, station, _pending);
                break;

            case StationType.Shipping:
                _orders.TryDeliver(State, _pending);
                break;

            case StationType.Recycle:
                if (State.CarriedItem == null) { _pending.Add(new SimEvent(SimEventType.Rejected, d.Message("nothing_to_recycle"))); break; }
                // A botched toy is worthless, so it goes straight in without asking.
                if (!State.RecyclePromptOpen && State.CarriedItem.Stage != ItemStage.Botched) { State.RecyclePromptOpen = true; _pending.Add(new SimEvent(SimEventType.RecyclePrompt)); break; }
                State.RecyclePromptOpen = false;
                State.CarriedItem = null;
                _pending.Add(new SimEvent(SimEventType.Recycled, d.Message("recycled")));
                break;
        }
    }

    private void EndShift()
    {
        CraftingSystem.CancelSilently(State, includeAssembly: true);
        State.RecyclePromptOpen = false;
        _orders.CloseUnfinished(State);
        State.Phase = RunPhase.Results;

        bool passed = State.Delivered >= Quota;
        var cue = Content.Dialogue.Cue(passed ? "review_pass" : "review_miss");
        _pending.Add(new SimEvent(SimEventType.ShiftEnded, cue?.Text, cue?.Santa));
    }

    /// <summary>Puts the elf at a point without walking there (tests and tutorial resets).</summary>
    public void PlacePlayer(Point2 p) => State.PlayerPosition = p;
}
