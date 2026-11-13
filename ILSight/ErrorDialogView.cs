using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.CommandArea;

namespace ILSight;

/// <summary>The recent errors: a list to pick one from when there are several, the picked one's
/// title over its details in a selectable monospace view, and Copy, Clear and Close.</summary>
internal static class ErrorDialogView
{
    /// <summary>Logs <paramref name="problem"/> as having happened at <paramref name="time"/>.</summary>
    public static void AddError(ErrorLog errors, AppProblem problem, DateTime time)
    {
        errors.Entries.Add(new AppError { Problem = problem, Time = time });
    }

    public static void Render(Ui<UiState> ui, ErrorLog errors)
    {
        var t = ui.State.Strings;
        var shown = ui.State.ErrorDialog;
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

        ItemSelections.ShowIn(shown.Selection, errors.Entries);
        if (errors.Entries.Count > 1)
        {
            List(ui, errors, shown);
        }

        var selected = shown.SelectedError;
        if (selected is not null)
        {
            using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center }))
            {
                ui.Glyph(ui.State.Icons.StatusError, theme.Danger, theme.IconSize);
                ui.Text(shown.Texts.GetText(selected, t).Title, new Box { Width = Sizing.Grow() }, bold: true, overflow: TextOverflow.Ellipsis);
            }
        }

        using (ui.Panel(new Box { Height = Sizing.Grow(), Padding = theme.BorderWidth, Background = theme.EditorBackground, Clip = true }))
        {
            CodeViewWidget.RenderCodeView(ui, "error-text", GetSelectedDocument(shown, t), shown.View);
        }

        using (ui.Row(new Box { Gap = theme.GapTight }))
        {
            if (ui.Button(t.ErrorsCopy, id: "error-copy") && selected is not null)
            {
                ui.ClipboardText = shown.Texts.GetText(selected, t).FullText;
            }

            ui.Tooltip(ui.GetId("error-copy"), t.ErrorsCopyTooltip);
            if (ui.Button(t.ErrorsClear, id: "error-clear", variant: ButtonVariant.Ghost))
            {
                Commands.Later(ui.State, shown, static (a, s) => ClearErrors(a.Errors, s));
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
        var error = dialog.SelectedError;
        if (error is null)
        {
            return DecompiledDocument.Empty;
        }

        return dialog.Texts.GetDocument(error, t);
    }

    /// <summary>Forgets every error and closes the dialog.</summary>
    private static void ClearErrors(ErrorLog errors, ErrorDialog shown)
    {
        errors.Entries.Clear();
        shown.IsOpen = false;
    }

    private static void List(Ui<UiState> ui, ErrorLog errors, ErrorDialog shown)
    {
        var t = ui.State.Strings;
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
        ItemSelections.TakeFrom(shown.Selection, entries);
    }
}
