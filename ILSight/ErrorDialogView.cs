using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.CommandArea;

namespace ILSight;

/// <summary>The recent errors: a list to pick one from when there are several, the picked one's
/// title over its details in a selectable monospace view, a "Technical details" box that shows its exception instead
/// when it has one, and Copy, Clear and Close.</summary>
internal static class ErrorDialogView
{
    /// <summary>Logs <paramref name="problem"/> as having happened at <paramref name="time"/>.</summary>
    public static void AddError(ErrorLog errors, AppProblem problem, DateTime time)
    {
        errors.Entries.Add(new AppError { Problem = problem, Time = time });
    }

    public static void Render(Ui ui, Appearance appearance, ErrorDialog errorDialog, ScreenDisplay display, IAppCommandWriter commands, ErrorLog errors)
    {
        var t = appearance.Strings;
        var shown = errorDialog;
        if (!shown.IsOpen)
        {
            return;
        }

        var theme = ui.Theme;
        var title = ui.Memo("errors-title", (errors.Entries.Count, t), static a => a.t.ErrorsTitle(a.Count));
        using var dialog = ui.Dialog("errors", title, ref shown.IsOpen, new Box
        {
            Width = Sizing.Grow(0f, AppLayout.ErrorDialogWidth),
            Height = Sizing.Grow(0f, AppLayout.ErrorDialogHeight),
        });
        if (!dialog.IsOpen)
        {
            return;
        }

        shown.Selection.ShowIn(errors.Entries);
        if (errors.Entries.Count > 1)
        {
            List(ui, appearance, errors, shown);
        }

        var selected = shown.Selection.Item;
        if (selected is not null)
        {
            using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center }))
            {
                ui.Glyph(appearance.Icons.StatusError, theme.Danger, theme.IconSize);
                ui.Text(shown.Texts.GetText(selected, t).Title, new Box { Width = Sizing.Grow() }, bold: true, overflow: TextOverflow.Ellipsis);
            }
        }

        using (ui.Panel(new Box { Height = Sizing.Grow(), Padding = theme.BorderWidth, Background = theme.EditorBackground, Clip = true }))
        {
            CodeViewWidget.RenderCodeView(ui, appearance, display, "error-text", GetSelectedDocument(shown, t), shown.View);
        }

        using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center }))
        {
            var technical = selected is null ? "" : shown.Texts.GetText(selected, t).TechnicalDetails;
            if (technical.Length > 0)
            {
                ui.Checkbox(t.ErrorsTechnicalDetails, ref shown.ShowsTechnicalDetails, id: "error-technical-details");
            }

            if (ui.Button(t.ErrorsCopy, id: "error-copy") && selected is not null)
            {
                ui.ClipboardText = shown.Texts.GetText(selected, t).FullText;
            }

            ui.Tooltip(ui.GetId("error-copy"), t.ErrorsCopyTooltip);
            if (ui.Button(t.ErrorsClear, id: "error-clear", variant: ButtonVariant.Ghost))
            {
                Commands.Later(commands, shown, static (a, s) => ClearErrors(a.Errors, s));
            }

            ui.Spacer();
            if (ui.Button(t.ErrorsClose, id: "error-close", variant: ButtonVariant.Primary))
            {
                shown.IsOpen = false;
            }
        }
    }

    private static DecompiledDocument GetSelectedDocument(ErrorDialog dialog, Strings t)
    {
        var error = dialog.Selection.Item;
        if (error is null)
        {
            return DecompiledDocument.Empty;
        }

        var technical = dialog.Texts.GetText(error, t).TechnicalDetails;
        if (dialog.ShowsTechnicalDetails && technical.Length > 0)
        {
            return dialog.Texts.GetTechnicalDocument(error, t);
        }

        return dialog.Texts.GetDocument(error, t);
    }

    private static void ClearErrors(ErrorLog errors, ErrorDialog shown)
    {
        errors.Entries.Clear();
        shown.IsOpen = false;
    }

    private static void List(Ui ui, Appearance appearance, ErrorLog errors, ErrorDialog shown)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var entries = errors.Entries;
        using var list = ui.SelectableList(ui.GetId("error-list"), entries.Count, shown.Selection.Rows, box: new Box
        {
            Height = Math.Min(entries.Count, AppLayout.ErrorListRows) * theme.RowHeight,
            Background = theme.Field,
        });
        for (var i = list.First; i < list.End; i++)
        {
            var entry = entries[i];
            using (ui.Id(i))
            using (var row = list.Row(i, ui.GetId("row"), new Box { Gap = theme.Gap }))
            {
                var titleColor = row.Selected ? theme.SelectionText : theme.Text;
                ui.Text(ui.Memo("time", (entry.Time, t), static a => a.Time.ToString("T", a.t.Culture)), color: theme.TextMuted);
                ui.Text(shown.Texts.GetText(entry, t).Title, new Box { Width = Sizing.Grow() }, color: titleColor, overflow: TextOverflow.Ellipsis);
            }
        }
        shown.Selection.TakeFrom(entries);
    }
}
