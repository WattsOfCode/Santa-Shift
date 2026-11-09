using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>
/// Building toys at the bench (a quick minigame) and tying ribbons (a timed action). The elf keeps the item the whole time.
///
/// Assembly: the bench shows the toy's parts, each on a number key, and the order they must go on. Each correct key
/// hammers the next part on. A wrong key, or running out of time, botches the toy: it can only be scrapped.
/// Early rounds use the toy's natural order; later rounds shuffle the order, then the keys too (see AssemblyTuning).
/// </summary>
public sealed class CraftingSystem(GameContent content, Random random)
{
    private readonly Tuning _t = content.Tuning;

    public void TryAssemble(RunState state, Station bench, List<SimEvent> output)
    {
        var d = content.Dialogue;
        var item = state.CarriedItem;
        if (item == null) { output.Add(Reject(d.Message("bench_needs_kit"))); return; }
        if (item.Stage == ItemStage.Botched) { output.Add(Reject(d.Message("botched_scrap_it"))); return; }
        if (item.IsCoal) { output.Add(Reject(d.Message("coal_not_a_kit"))); return; }
        if (item.Stage != ItemStage.Kit) { output.Add(Reject(d.Message("already_assembled"))); return; }

        var parts = content.Toy(item.ToyId).Parts.Select(p => p.Id).ToList();
        var a = _t.Assembly;
        var order = a.ShuffleOrder(state.Mode, state.Round) ? Shuffled(parts) : parts;
        var keys = parts;
        if (a.ShuffleKeys(state.Mode, state.Round))
        {
            // New key layout, but never one where the keys simply go 1, 2, 3 again.
            for (int tries = 0; tries < 10 && (keys == parts || keys.SequenceEqual(order)); tries++) keys = Shuffled(parts);
        }

        item.PartsPlaced = 0;
        state.Assembly = new AssemblyState
        {
            StationId = bench.Id,
            ToyId = item.ToyId,
            ItemRevision = item.Revision,
            Keys = keys,
            Order = order,
            StartTick = state.Tick,
            DeadlineTick = state.Tick + _t.Ticks(a.TimeLimitFor(state.Mode, state.Round) + state.Modifiers.BenchSeconds),
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

        if (partKey >= 1 && partKey <= game.Keys.Count)
        {
            string pressed = game.Keys[partKey - 1];
            if (pressed != game.NextPart) { Botch(state, item, d.Message("botched_wrong_part", PartName(item.ToyId, pressed)), output); return; }

            game.Placed++;
            item.PartsPlaced = game.Placed;
            output.Add(new SimEvent(SimEventType.PartAdded, PartName(item.ToyId, pressed), ToyId: item.ToyId, Points: game.Placed));

            if (game.NextPart == null)
            {
                state.Assembly = null;
                item.Stage = ItemStage.Assembled;
                item.Revision = state.NextId();
                output.Add(new SimEvent(SimEventType.AssemblyComplete, ToyId: item.ToyId));
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

    /// <summary>A random order that differs from the natural one whenever there is more than one part.</summary>
    private List<string> Shuffled(List<string> parts)
    {
        if (parts.Count < 2) return parts.ToList();
        List<string> result;
        do
        {
            result = parts.ToList();
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
        } while (result.SequenceEqual(parts));
        return result;
    }

    private static SimEvent Reject(string text) => new(SimEventType.Rejected, text);
}
