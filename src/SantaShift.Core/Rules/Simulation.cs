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
    /// <param name="naughty">Children on the run's naughty list: their orders want coal from the fireplace.</param>
    /// <param name="modifiers">The run's perks, combined (see <see cref="RunProgress.Modifiers"/>).</param>
    public Simulation(GameContent content, GameMode mode, int round = 1, int? seed = null, IEnumerable<string>? naughty = null,
        RunModifiers? modifiers = null, double? spirit = null, bool tutorial = false)
    {
        Content = content;
        State = new RunState
        {
            Mode = mode, Round = Math.Max(1, round), PlayerPosition = content.PlayerStart,
            NaughtyChildren = (naughty ?? []).ToHashSet(),
            LimitedKits = content.HasSupply,
            Modifiers = modifiers ?? RunModifiers.None,
            Tutorial = tutorial,
            Spirit = Math.Clamp(spirit ?? content.Tuning.Spirit.Start, 0, content.Tuning.Spirit.Max),
        };
        _motion = new PlayerMotion(content);
        _crafting = new CraftingSystem(content, new Random(seed ?? State.Round * 7919 + (int)mode));
        _orders = new OrderSystem(content);
        _events = new EventSystem(content);
        _orders.PlanShift(State); // Santa's sack for the shift: decides the letters and fills the kit bins once
    }

    public Tuning Tuning => Content.Tuning;
    public long RemainingShiftTicks => Math.Max(0, Tuning.ShiftTicks - State.Tick);
    public int Quota => Tuning.QuotaFor(State.Mode, State.Round);

    public void Step(PlayerCommand cmd)
    {
        if (State.Phase != RunPhase.Playing) return;

        if (State.Tick >= Tuning.ShiftTicks) { EndShift(); return; }
        int firstEvent = _pending.Count;

        _events.Update(State, _pending);
        _orders.SpawnScheduled(State, _pending);

        _motion.Move(State, cmd.MoveX, cmd.MoveY);
        _crafting.Update(State, cmd.IsMoving, cmd.PartKey, _pending);
        if (cmd.IsMoving || cmd.Cancel) State.RecyclePromptOpen = false;

        if (cmd.Interact) Interact();

        _orders.Expire(State, _pending);
        State.Tick++;
        if (UpdateSpirit(firstEvent)) EndShift(spiritOut: true);
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
        if (State.RibbonPick != null) return d.Message("picking_ribbon");
        if (State.CraftingAction is { } timed)
            return timed.Type == CraftingType.Shovel ? d.Message("working_shovel") : d.Message("working_ribbon", ToyName(item));
        if (State.RecyclePromptOpen) return d.Message("prompt_recycle_confirm", ItemName(item));
        if (station == null) return null;
        bool botched = item is { Stage: ItemStage.Botched };
        if (botched && station.Type is StationType.Bench or StationType.Ribbon or StationType.Shipping)
            return d.Message("botched_scrap_it");
        if (item is { IsCoal: true } && station.Type is StationType.Bench or StationType.Ribbon)
            return d.Message(station.Type == StationType.Bench ? "coal_not_a_kit" : "no_ribbon_for_coal");
        if (item is { IsSupply: true } && station.Type is StationType.Bench or StationType.Ribbon or StationType.Shipping)
            return d.Message("supply_goes_to_bin");

        return station.Type switch
        {
            StationType.Dispenser => item is { IsSupply: true } ? d.Message("prompt_restock", Content.Toy(station.ToyId!).Name)
                : item != null ? d.Message("hands_full")
                : State.KitsLeft(station.ToyId!) is <= 0 ? d.Message("bin_empty", Content.Toy(station.ToyId!).Name)
                : d.Message("prompt_collect", Content.Toy(station.ToyId!).Name),
            StationType.Supply => item == null ? d.Message("prompt_supply") : d.Message("hands_full"),
            StationType.Bench => item is { Stage: ItemStage.Kit }
                ? d.Message("prompt_assemble", ToyName(item))
                : d.Message(item == null ? "bench_needs_kit" : "already_assembled"),
            StationType.Ribbon => item is { Stage: ItemStage.Assembled, HasRibbon: false }
                ? d.Message("prompt_ribbon", ToyName(item))
                : d.Message(item == null ? "ribbon_needs_toy" : item.HasRibbon ? "already_ribboned" : "assemble_first"),
            StationType.Coal => item == null ? d.Message("prompt_coal") : d.Message("hands_full"),
            StationType.Shipping => item is { Stage: ItemStage.Assembled or ItemStage.Coal }
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
        if (item.IsCoal) return Content.Dialogue.Message("item_coal");
        if (item.IsSupply) return Content.Dialogue.Message("item_supply");
        string name = ToyName(item);
        if (item.Stage == ItemStage.Kit) return Content.Dialogue.Message("item_kit", name);
        if (item.Stage == ItemStage.Botched) return Content.Dialogue.Message("item_botched", name);
        if (!item.HasRibbon) return name;
        return Tuning.FindRibbon(item.RibbonColor) is { } colour
            ? Content.Dialogue.Message("item_ribboned_colour", name, colour.Name.ToLowerInvariant())
            : Content.Dialogue.Message("item_ribboned", name);
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
                UseKitBin(station);
                break;

            case StationType.Supply:
                if (State.CarriedItem != null) { _pending.Add(new SimEvent(SimEventType.Rejected, d.Message("hands_full"))); break; }
                State.CarriedItem = Item.Supply(State.NextId());
                _pending.Add(new SimEvent(SimEventType.SupplyTaken));
                break;

            case StationType.Coal:
                _crafting.TryShovel(State, station, _pending);
                break;

            case StationType.Bench or StationType.Ribbon or StationType.Shipping when State.CarriedItem is { IsSupply: true }:
                _pending.Add(new SimEvent(SimEventType.Rejected, d.Message("supply_goes_to_bin")));
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
                // A botched toy or a lump of coal is worth nothing, so it goes straight in without asking.
                if (!State.RecyclePromptOpen && State.CarriedItem.Stage is not (ItemStage.Botched or ItemStage.Coal or ItemStage.Supply)) { State.RecyclePromptOpen = true; _pending.Add(new SimEvent(SimEventType.RecyclePrompt)); break; }
                State.RecyclePromptOpen = false;
                State.CarriedItem = null;
                _pending.Add(new SimEvent(SimEventType.Recycled, d.Message("recycled")));
                break;
        }
    }

    /// <summary>
    /// A kit bin: hands out a kit (one less in stock when bins are limited), or takes a supply crate and restocks.
    /// </summary>
    private void UseKitBin(Station bin)
    {
        var d = Content.Dialogue;
        string toyId = bin.ToyId!;
        string name = Content.FindToy(toyId)?.Name ?? toyId;
        if (State.CarriedItem is { IsSupply: true })
        {
            State.CarriedItem = null;
            int kits = Tuning.ResupplyKits + State.Modifiers.ExtraSupplyKits;
            State.KitStock[toyId] = State.KitStock.GetValueOrDefault(toyId) + kits;
            _pending.Add(new SimEvent(SimEventType.Restocked, d.Message("restocked", name, kits), ToyId: toyId));
            return;
        }
        if (State.CarriedItem != null) { _pending.Add(new SimEvent(SimEventType.Rejected, d.Message("hands_full"))); return; }
        if (State.KitsLeft(toyId) is <= 0) { _pending.Add(new SimEvent(SimEventType.Rejected, d.Message("bin_empty", name), ToyId: toyId)); return; }

        if (State.LimitedKits) State.KitStock[toyId]--;
        State.CarriedItem = new Item { ToyId = toyId, Revision = State.NextId() };
        _pending.Add(new SimEvent(SimEventType.Collected, ToyId: toyId));
    }

    /// <summary>
    /// The Christmas Spirit meter reacts to what happened this step. Returns true when it has just run out.
    /// </summary>
    private bool UpdateSpirit(int firstEvent)
    {
        var s = Tuning.Spirit;
        if (!s.Enabled) return false;
        double change = 0;
        for (int i = firstEvent; i < _pending.Count; i++)
        {
            var e = _pending[i];
            change += e.Type switch
            {
                SimEventType.Delivered => e.ToyId == Item.CoalId ? s.CoalDelivered : s.Delivered,
                SimEventType.OrderExpired => -s.Missed,
                SimEventType.AssemblyBotched => -s.Botched,
                _ => 0,
            };
        }
        if (change == 0) return false;
        State.Spirit = Math.Clamp(State.Spirit + change, 0, s.Max);
        return State.Spirit <= 0;
    }

    private void EndShift(bool spiritOut = false)
    {
        State.SpiritOut = spiritOut;
        CraftingSystem.CancelSilently(State, includeAssembly: true);
        State.RecyclePromptOpen = false;
        _orders.CloseUnfinished(State);
        State.Phase = RunPhase.Results;

        bool passed = State.Delivered >= Quota && !spiritOut;
        var cue = Content.Dialogue.Cue(spiritOut ? "spirit_out" : passed ? "review_pass" : "review_miss");
        _pending.Add(new SimEvent(SimEventType.ShiftEnded, cue?.Text, cue?.Santa));
    }

    /// <summary>Puts the elf at a point without walking there (tests and tutorial resets).</summary>
    public void PlacePlayer(Point2 p) => State.PlayerPosition = p;
}
