using Raylib_cs;
using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;
using SantaShift.Game.Views.World;

namespace SantaShift.Game.Controllers;

/// <summary>
/// Between rounds: Santa offers a few perk cards and the player keeps one for the rest of the run, then the next round
/// starts. Left / right (or the mouse) to choose, confirm (or a click) to take it.
/// </summary>
public sealed class PerkPickController : IScreen
{
    private readonly GameServices _s;
    private readonly RunProgress _run;
    private readonly WorldView _world;
    private readonly PerkPickView _view;
    private int _selected;
    private System.Numerics.Vector2 _lastPointer;

    public IReadOnlyList<Perk> Offers { get; }

    /// <summary>True for the head start: the pick comes before round 1, so no round is cleared yet.</summary>
    public bool BeforeFirstRound { get; }

    public PerkPickController(GameServices services, RunProgress run, bool beforeFirstRound = false)
    {
        _s = services;
        _run = run;
        BeforeFirstRound = beforeFirstRound;
        _world = new WorldView(services.Content, services.Assets, services.World);
        _view = new PerkPickView();
        var t = services.Content.Tuning;
        var pool = services.Progress.AvailablePerks(services.Content.Perks, services.Content.Unlocks);
        Offers = run.OfferPerks(pool, t.PerkOffers, t.BonusPerkMargin, Random.Shared.Next());
    }

    /// <summary>The screen after a passed round: the perk pick if there is anything to offer, otherwise the next round.</summary>
    public static IScreen After(GameServices services, RunProgress run)
    {
        var pick = new PerkPickController(services, run);
        if (pick.Offers.Count > 0) return pick;
        run.StartNextRound();
        return new WorkshopController(services, run);
    }

    public void FixedUpdate()
    {
        var input = _s.Input;
        if (input.ConsumePressed(InputAction.MenuLeft)) Move(-1);
        if (input.ConsumePressed(InputAction.MenuRight)) Move(+1);

        // The mouse picks a card only when it moves, so it never fights the arrow keys.
        var pointer = PointerUi();
        int hovered = _view.CardAt(pointer, Offers.Count);
        if (hovered >= 0 && System.Numerics.Vector2.Distance(pointer, _lastPointer) > 1) _selected = hovered;
        _lastPointer = pointer;

        bool click = input.ConsumePressed(InputAction.Click) && hovered >= 0;
        if (click) Take(Offers[hovered]);
        else if (input.ConsumePressed(InputAction.Confirm)) Take(Offers[_selected]);
    }

    private void Move(int step)
    {
        _selected = (_selected + step + Offers.Count) % Offers.Count;
        _s.Audio.Play(SoundIds.UiMove);
    }

    private void Take(Perk perk)
    {
        _s.Audio.Play(SoundIds.PerkPick);
        _run.AddPerk(perk);
        if (!BeforeFirstRound) _run.StartNextRound();
        _s.Scenes.Switch(new WorkshopController(_s, _run));
    }

    private System.Numerics.Vector2 PointerUi() =>
        UiCanvas.FromScreen(_s.Input.Pointer(), Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

    public void DrawWorld() => _world.DrawRoom();

    public void DrawUi(UiCanvas ui)
    {
        var t = _s.Content.Tuning;
        _view.Draw(ui, _s.Content.Dialogue, _run, Offers, _selected, Offers.Count > t.PerkOffers && !BeforeFirstRound,
            BeforeFirstRound, t.QuotaFor(_run.Mode, BeforeFirstRound ? 1 : _run.Round + 1), _s.Input, Raylib.GetTime(), _s.Settings.ReducedMotion);
    }
}
