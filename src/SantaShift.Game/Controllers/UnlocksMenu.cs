using SantaShift.Core.Models;
using SantaShift.Game.App;
using SantaShift.Game.Services;
using SantaShift.Game.Views.UI;

namespace SantaShift.Game.Controllers;

/// <summary>
/// The unlocks list from the title: each unlock with its progress, and the hat the elf wears (any unlocked hat, or the
/// classic red). Up / down to choose, confirm to wear a hat, back to close (the choice is saved).
/// </summary>
public sealed class UnlocksMenu(GameServices s)
{
    private readonly UnlocksView _view = new();
    private readonly MenuMouse _mouse = new(s.Input);
    private bool _closeHovered;

    public bool IsOpen { get; private set; }

    /// <summary>The selected row: 0 is the classic red hat, then the unlocks in order.</summary>
    public int Row { get; private set; }

    public void Open()
    {
        IsOpen = true;
        Row = 0;
        _view.Hits.Clear();
        s.Input.Clear();
    }

    public void Update()
    {
        if (!IsOpen) return;
        var input = s.Input;
        int rows = s.Content.Unlocks.Count + 1;
        _mouse.Update();
        int hit = _view.Hits.At(_mouse.At);
        _closeHovered = hit == UnlocksView.CloseId;
        if (input.ConsumePressed(InputAction.Back) || (_mouse.Clicked && _closeHovered))
        {
            IsOpen = false;
            s.SaveProgress();
            s.Input.Clear();
            return;
        }
        bool onRow = hit >= 0 && hit < rows;
        if (_mouse.Moved && onRow && hit != Row) { Row = hit; s.Audio.Play(SoundIds.UiMove); }
        if (input.ConsumePressed(InputAction.MenuUp)) { Row = (Row + rows - 1) % rows; s.Audio.Play(SoundIds.UiMove); }
        if (input.ConsumePressed(InputAction.MenuDown)) { Row = (Row + 1) % rows; s.Audio.Play(SoundIds.UiMove); }
        bool clicked = _mouse.Clicked && onRow;
        if (clicked) Row = hit;
        if (!clicked && !input.ConsumePressed(InputAction.Confirm)) return;

        if (Row == 0) { s.Progress.Hat = ""; s.Audio.Play(SoundIds.UiConfirm); return; }
        var unlock = s.Content.Unlocks[Row - 1];
        if (unlock.Reward == UnlockReward.Hat && s.Progress.Has(unlock.Id))
        {
            s.Progress.Hat = unlock.Id;
            s.Audio.Play(SoundIds.UiConfirm);
        }
        else s.Audio.Play(SoundIds.ActionRejected);
    }

    public void Draw(UiCanvas ui)
    {
        if (IsOpen) _view.Draw(ui, s.Content.Unlocks, s.Progress, Row, s.Input, _closeHovered);
    }
}
