using SantaShift.Core.Content;
using SantaShift.Core.Models;

namespace SantaShift.Core.Rules;

/// <summary>Timed assembly and ribbon actions (GDD section 8). The elf keeps the item the whole time.</summary>
public sealed class CraftingSystem(GameContent content)
{
    private readonly Tuning _t = content.Tuning;

    public void TryAssemble(RunState state, Station bench, List<SimEvent> output)
    {
        var d = content.Dialogue;
        var item = state.CarriedItem;
        if (item == null) { output.Add(Reject(d.Message("bench_needs_kit"))); return; }
        if (item.Stage != ItemStage.Kit) { output.Add(Reject(d.Message("already_assembled"))); return; }

        Start(state, bench, CraftingType.Assemble, content.Toy(item.ToyId).AssembleSeconds);
        output.Add(new SimEvent(SimEventType.AssemblyStarted, ToyId: item.ToyId));
    }

    public void TryRibbon(RunState state, Station station, List<SimEvent> output)
    {
        var d = content.Dialogue;
        var item = state.CarriedItem;
        if (item == null) { output.Add(Reject(d.Message("ribbon_needs_toy"))); return; }
        if (item.Stage != ItemStage.Assembled) { output.Add(Reject(d.Message("assemble_first"))); return; }
        if (item.HasRibbon) { output.Add(Reject(d.Message("already_ribboned"))); return; }

        Start(state, station, CraftingType.Ribbon, _t.RibbonSeconds);
        output.Add(new SimEvent(SimEventType.RibbonStarted, ToyId: item.ToyId));
    }

    /// <summary>Advances the current action. Moving cancels it; the item is never lost or changed by a cancel.</summary>
    public void Update(RunState state, bool moving, List<SimEvent> output)
    {
        var action = state.CraftingAction;
        if (action == null) return;

        var item = state.CarriedItem;
        if (item == null || item.Revision != action.ItemRevision) { state.CraftingAction = null; return; } // stale

        if (moving)
        {
            state.CraftingAction = null;
            string key = action.Type == CraftingType.Assemble ? "assembly_cancelled" : "ribbon_cancelled";
            output.Add(new SimEvent(SimEventType.ActionCancelled, content.Dialogue.Message(key), ToyId: item.ToyId));
            return;
        }

        if (state.Tick < action.CompletionTick) return;

        state.CraftingAction = null;
        item.Revision = state.NextId();
        if (action.Type == CraftingType.Assemble)
        {
            item.Stage = ItemStage.Assembled;
            output.Add(new SimEvent(SimEventType.AssemblyComplete, ToyId: item.ToyId));
        }
        else
        {
            item.HasRibbon = true;
            output.Add(new SimEvent(SimEventType.RibbonComplete, ToyId: item.ToyId));
        }
    }

    /// <summary>Cancels without a message (used by pause and shift end).</summary>
    public static void CancelSilently(RunState state) => state.CraftingAction = null;

    private void Start(RunState state, Station station, CraftingType type, double seconds)
    {
        state.CraftingAction = new CraftingAction
        {
            StationId = station.Id,
            Type = type,
            ItemRevision = state.CarriedItem!.Revision,
            StartTick = state.Tick,
            CompletionTick = state.Tick + _t.Ticks(seconds),
        };
    }

    private static SimEvent Reject(string text) => new(SimEventType.Rejected, text);
}
