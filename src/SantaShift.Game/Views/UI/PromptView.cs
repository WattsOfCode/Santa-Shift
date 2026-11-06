namespace SantaShift.Game.Views.UI;

/// <summary>The centre of the bottom bar: a recent message if there is one, otherwise the contextual action.</summary>
public sealed class PromptView
{
    public void Draw(UiCanvas ui, string? prompt, Feedback feedback, float cx, float y)
    {
        if (feedback.Toast != null)
        {
            var color = feedback.ToastKind switch
            {
                ToastKind.Success => Palette.Gold,
                ToastKind.Rejected => Palette.Berry,
                _ => Palette.Snow,
            };
            float w = ui.MeasureText(feedback.Toast, 20) + 24;
            ui.FillRounded(cx - w / 2, y, w, 30, Palette.WithAlpha(color, 235), 0.4f);
            ui.Text(feedback.Toast, cx, y + 5, 20, feedback.ToastKind == ToastKind.Info ? Palette.Ink : Palette.Ink, Align.Center);
            return;
        }

        if (string.IsNullOrEmpty(prompt)) return;
        float pw = ui.MeasureText(prompt, 20) + 24;
        ui.FillRounded(cx - pw / 2, y, pw, 30, Palette.WithAlpha(Palette.Snow, 40), 0.4f);
        ui.Text(prompt, cx, y + 5, 20, Palette.Snow, Align.Center);
    }
}
