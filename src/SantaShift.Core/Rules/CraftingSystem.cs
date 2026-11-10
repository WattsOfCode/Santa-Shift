using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>
/// Building toys at the bench (a quick minigame) and tying ribbons (a timed action). The elf keeps the item the whole time.
///
/// Assembly: every toy has fixed build combinations (toys.json): the plain toy, and from a later round its variants
/// (a big-wheel train, a big bear, a rocket robot). Each part always sits on the same part key for that toy, so a
/// combination is a fixed key sequence the player can learn. Each key that continues a combination hammers that part
/// on; the parts pressed decide which version gets built. A key that fits no combination, or running out of time,
/// botches the toy: it can only be scrapped.
/// </summary>
public sealed class CraftingSystem(GameContent content)
{
    private readonly Tuning _t = content.Tuning;

    /// <summary>The combinations a toy can be built with in this round: the plain toy, plus the variants letters can ask for.</summary>
    public IReadOnlyList<ToyVariant> RecipesFor(Toy toy, RunState state) =>
        toy.Recipes.Where((r, i) => i == 0 || _t.Assembly.VariantOpen(r, state.Mode, state.Round, state.Tutorial)).ToList();

    public void TryAssemble(RunState state, Station bench, List<SimEvent> output)
    {
        var d = content.Dialogue;
        var item = state.CarriedItem;
        if (item == null) { output.Add(Reject(d.Message("bench_needs_kit"))); return; }
        if (item.Stage == ItemStage.Botched) { output.Add(Reject(d.Message("botched_scrap_it"))); return; }
        if (item.IsCoal) { output.Add(Reject(d.Message("coal_not_a_kit"))); return; }
        if (item.Stage != ItemStage.Kit) { output.Add(Reject(d.Message("already_assembled"))); return; }

        var toy = content.Toy(item.ToyId);
        var recipes = RecipesFor(toy, state);
        // The parts used by this round's combinations, each on its own key (a key no combination uses stays empty).
        var used = recipes.SelectMany(r => r.Order).ToHashSet();
        int keyCount = used.Count == 0 ? 0 : used.Max(toy.KeyOf);
        var keys = new string[keyCount];
        for (int k = 0; k < keyCount; k++) keys[k] = "";
        foreach (var id in used) keys[toy.KeyOf(id) - 1] = id;

        // The most urgent nice letter for this toy decides which combination the bench suggests.
        var letter = state.LiveOrders.Where(o => !o.Naughty && o.ToyId == item.ToyId)
            .OrderBy(o => o.DeadlineTick).ThenBy(o => o.Id).FirstOrDefault();
        var wanted = letter?.Variant;
        if (wanted != null && !recipes.Any(r => r.Id == wanted && r != toy.Standard)) wanted = null;

        int longest = recipes.Max(r => r.Order.Count);
        item.PartsPlaced = 0;
        state.Assembly = new AssemblyState
        {
            StationId = bench.Id,
            ToyId = item.ToyId,
            ItemRevision = item.Revision,
            Keys = keys,
            Recipes = recipes,
            Wanted = wanted,
            LetterWaiting = letter != null,
            StartTick = state.Tick,
            DeadlineTick = state.Tick + _t.Ticks(_t.Assembly.TimeLimitFor(state.Mode, state.Round, longest) + state.Modifiers.BenchSeconds),
        };
        output.Add(new SimEvent(SimEventType.AssemblyStarted, ToyId: item.ToyId));
    }

    public void TryRibbon(RunState state, Station station, List<SimEvent> output)
    {
        var d = content.Dialogue;
        var item = state.CarriedItem;
        if (item == null) { output.Add(Reject(d.Message("ribbon_needs_toy"))); return; }
        if (item.Stage == ItemStage.Botched) { output.Add(Reject(d.Message("botched_scrap_it"))); return; }
        if (item.IsCoal) { output.Add(Reject(d.Message("no_ribbon_for_coal"))); return; }
        if (item.Stage != ItemStage.Assembled) { output.Add(Reject(d.Message("assemble_first"))); return; }
        if (item.HasRibbon && item.Revision == state.FreshRibbonRevision) return; // just tied: the prompt already says so
        if (item.HasRibbon) { output.Add(Reject(d.Message("already_ribboned"))); return; }

        // With ribbon colours, the elf first picks a spool (part keys, or a click on it); otherwise tying starts now.
        if (_t.RibbonColors.Length > 0)
        {
            state.RibbonPick = new RibbonPickState { StationId = station.Id, ItemRevision = item.Revision, StartTick = state.Tick };
            output.Add(new SimEvent(SimEventType.RibbonPickStarted, ToyId: item.ToyId));
            return;
        }
        StartTying(state, station.Id, item, null, output);
    }

    private void StartTying(RunState state, string stationId, Item item, string? colour, List<SimEvent> output)
    {
        state.CraftingAction = new CraftingAction
        {
            StationId = stationId,
            Type = CraftingType.Ribbon,
            ItemRevision = item.Revision,
            StartTick = state.Tick,
            CompletionTick = state.Tick + _t.Ticks(_t.RibbonSeconds * state.Modifiers.RibbonTimeMultiplier),
            RibbonColor = colour,
        };
        output.Add(new SimEvent(SimEventType.RibbonStarted, _t.FindRibbon(colour)?.Name, ToyId: item.ToyId));
    }

    /// <summary>
    /// Advances the minigame and the ribbon. Moving cancels either one and keeps the item as it was before
    /// (a half-built kit goes back to a plain kit).
    /// </summary>
    public void Update(RunState state, bool moving, int partKey, List<SimEvent> output)
    {
        UpdateAssembly(state, moving, partKey, output);
        UpdateRibbonPick(state, moving, partKey, output);
        UpdateRibbon(state, moving, output);
    }

    private void UpdateAssembly(RunState state, bool moving, int partKey, List<SimEvent> output)
    {
        var game = state.Assembly;
        if (game == null) return;
        var d = content.Dialogue;

        var item = state.CarriedItem;
        if (item == null || item.Revision != game.ItemRevision) { state.Assembly = null; return; } // stale

        if (moving)
        {
            state.Assembly = null;
            item.PartsPlaced = 0;
            output.Add(new SimEvent(SimEventType.ActionCancelled, d.Message("assembly_cancelled"), ToyId: item.ToyId));
            return;
        }

        // A key with no part on it (e.g. 4 on a toy with three parts) does nothing.
        if (partKey >= 1 && partKey <= game.Keys.Count && game.Keys[partKey - 1] is { Length: > 0 } pressed)
        {
            // The part must continue at least one combination: what has gone on so far, then this part.
            var fitting = game.Recipes.Where(r => game.Fits(r) && r.Order[game.Placed] == pressed).ToList();
            if (fitting.Count == 0) { Botch(state, item, d.Message("botched_wrong_part", PartName(item.ToyId, pressed)), output); return; }

            game.PlacedParts.Add(pressed);
            item.PartsPlaced = game.Placed;
            output.Add(new SimEvent(SimEventType.PartAdded, PartName(item.ToyId, pressed), ToyId: item.ToyId, Points: game.Placed));

            // No combination is the start of another (checked when content loads), so at most one is now complete.
            if (fitting.FirstOrDefault(r => r.Order.Count == game.Placed) is { } built)
            {
                state.Assembly = null;
                item.Stage = ItemStage.Assembled;
                item.Variant = game.StoredId(built);
                item.Revision = state.NextId();
                output.Add(new SimEvent(SimEventType.AssemblyComplete, built.Name, ToyId: item.ToyId));
            }
            return;
        }

        if (state.Tick >= game.DeadlineTick) Botch(state, item, d.Message("botched_too_slow"), output);
    }

    /// <summary>Choosing a ribbon: part key n takes spool n and tying starts. Moving away cancels.</summary>
    private void UpdateRibbonPick(RunState state, bool moving, int partKey, List<SimEvent> output)
    {
        var pick = state.RibbonPick;
        if (pick == null) return;
        var item = state.CarriedItem;
        if (item == null || item.Revision != pick.ItemRevision) { state.RibbonPick = null; return; } // stale

        if (moving)
        {
            state.RibbonPick = null;
            output.Add(new SimEvent(SimEventType.ActionCancelled, content.Dialogue.Message("ribbon_cancelled"), ToyId: item.ToyId));
            return;
        }
        if (partKey < 1 || partKey > _t.RibbonColors.Length) return;
        state.RibbonPick = null;
        StartTying(state, pick.StationId, item, _t.RibbonColors[partKey - 1].Id, output);
    }

    /// <summary>Starts shovelling coal at the fireplace. The coal lands in the elf's hands when the scoop finishes.</summary>
    public void TryShovel(RunState state, Station scuttle, List<SimEvent> output)
    {
        if (state.CarriedItem != null) { output.Add(Reject(content.Dialogue.Message("hands_full"))); return; }
        state.CraftingAction = new CraftingAction
        {
            StationId = scuttle.Id,
            Type = CraftingType.Shovel,
            StartTick = state.Tick,
            CompletionTick = state.Tick + _t.Ticks(_t.ShovelSeconds * state.Modifiers.ShovelTimeMultiplier),
        };
        output.Add(new SimEvent(SimEventType.ShovelStarted, ToyId: Item.CoalId));
    }

    private void UpdateShovel(RunState state, bool moving, List<SimEvent> output)
    {
        var action = state.CraftingAction;
        if (state.CarriedItem != null) { state.CraftingAction = null; return; } // stale
        if (moving)
        {
            state.CraftingAction = null;
            output.Add(new SimEvent(SimEventType.ActionCancelled, content.Dialogue.Message("shovel_cancelled"), ToyId: Item.CoalId));
            return;
        }
        if (state.Tick < action!.CompletionTick) return;

        state.CraftingAction = null;
        state.CarriedItem = Item.Coal(state.NextId());
        output.Add(new SimEvent(SimEventType.CoalCollected, ToyId: Item.CoalId));
    }

    private void UpdateRibbon(RunState state, bool moving, List<SimEvent> output)
    {
        var action = state.CraftingAction;
        if (action == null) return;
        if (action.Type == CraftingType.Shovel) { UpdateShovel(state, moving, output); return; }

        var item = state.CarriedItem;
        if (item == null || item.Revision != action.ItemRevision) { state.CraftingAction = null; return; } // stale

        if (moving)
        {
            state.CraftingAction = null;
            output.Add(new SimEvent(SimEventType.ActionCancelled, content.Dialogue.Message("ribbon_cancelled"), ToyId: item.ToyId));
            return;
        }

        if (state.Tick < action.CompletionTick) return;

        state.CraftingAction = null;
        item.Revision = state.NextId();
        item.HasRibbon = true;
        item.RibbonColor = action.RibbonColor;
        state.FreshRibbonRevision = item.Revision;
        output.Add(new SimEvent(SimEventType.RibbonComplete, ToyId: item.ToyId));
    }

    /// <summary>
    /// Pausing cancels a ribbon (the item is kept). The minigame simply freezes with the clock, but it is
    /// cancelled at the end of the shift.
    /// </summary>
    public static void CancelSilently(RunState state, bool includeAssembly)
    {
        state.CraftingAction = null;
        state.RibbonPick = null;
        if (!includeAssembly || state.Assembly == null) return;
        state.Assembly = null;
        if (state.CarriedItem is { Stage: ItemStage.Kit } kit) kit.PartsPlaced = 0;
    }

    public string PartName(string toyId, string partId) =>
        content.FindToy(toyId)?.Parts.FirstOrDefault(p => p.Id == partId)?.Name ?? partId;

    private void Botch(RunState state, Item item, string text, List<SimEvent> output)
    {
        state.Assembly = null;
        item.Stage = ItemStage.Botched;
        item.Revision = state.NextId();
        output.Add(new SimEvent(SimEventType.AssemblyBotched, text, ToyId: item.ToyId));
    }

    private static SimEvent Reject(string text) => new(SimEventType.Rejected, text);
}
