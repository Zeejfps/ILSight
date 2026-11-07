using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;

namespace ILSight;

/// <summary>The recent errors: a list to pick one from when there are several, the picked one's
/// title over its details in a selectable monospace view, and Copy, Clear and Close.</summary>
internal static class ErrorDialogView
{
    /// <summary>Logs <paramref name="problem"/> as having happened at <paramref name="time"/>.</summary>
    public static void AddError(ErrorLog errors, AppProblem problem, DateTime time)
    {
        errors.Entries.Add(new AppError(problem, time));
    }

    /// <summary>Opens the dialog on the newest of <paramref name="errors"/>, unless there are none.</summary>
    public static void Open(ErrorDialog dialog, IReadOnlyList<AppError> errors)
    {
        if (errors.Count == 0)
            return;
        ItemSelections.Select(dialog.Selection, errors[^1], errors);
        dialog.IsOpen = true;
    }

    /// <summary>Opens the dialog on the newest of <paramref name="errors"/> when an error was added since
    /// it last saw the log, at <paramref name="revision"/>.</summary>
    public static void ShowNewErrors(ErrorDialog dialog, int revision, IReadOnlyList<AppError> errors)
    {
        if (revision == dialog.ShownRevision)
            return;
        dialog.ShownRevision = revision;
        Open(dialog, errors);
    }

    /// <summary><paramref name="error"/> in the words of <paramref name="t"/>, put into them once per language.</summary>
    public static ProblemText GetText(ErrorDialog dialog, AppError error, Strings t)
    {
        if (!dialog.Texts.TryGet(new(t), out var texts))
        {
            texts = new Dictionary<AppError, ProblemText>(ReferenceEqualityComparer.Instance);
            dialog.Texts = new(new(t), texts);
            dialog.Document = default;
        }
        if (!texts.TryGetValue(error, out var text))
        {
            text = ProblemTexts.Describe(t, error.Problem);
            texts[error] = text;
        }
        return text;
    }

    /// <summary>The selected error's details, or its title when it has none, in the words of
    /// <paramref name="t"/>, as a document for the dialog's code view.</summary>
    private static DecompiledDocument GetSelectedDocument(ErrorDialog dialog, Strings t)
    {
        var error = dialog.SelectedError;
        if (error == null)
            return DecompiledDocument.Empty;
        var text = GetText(dialog, error, t);
        if (dialog.Document.TryGet(new(error), out var document))
            return document;
        document = CreateDocument(text);
        dialog.Document = new(new(error), document);
        return document;
    }

    private static DecompiledDocument CreateDocument(ProblemText text)
    {
        var body = text.Details;
        if (body.Length == 0)
            body = text.Title;
        return Documents.CreateFromText(text.Title, body);
    }

    public static void Render(Ui<UiState> ui, ErrorLog errors)
    {
        var t = ui.State.Strings;
        var shown = ui.State.ErrorDialog;
        if (!shown.IsOpen)
            return;
        var theme = ui.Theme;
        var title = ui.Memo("errors-title", (errors.Entries.Count, t), static a => a.t.ErrorsTitle(a.Count));
        using var dialog = ui.Dialog("errors", title, ref shown.IsOpen, new Box
        {
            Width = Sizing.Grow(0f, AppLayout.ErrorDialogWidth),
            Height = Sizing.Grow(0f, AppLayout.ErrorDialogHeight),
        });
        if (!dialog.IsOpen) return;

        ItemSelections.ShowIn(shown.Selection, errors.Entries);
        if (errors.Entries.Count > 1)
            List(ui, errors, shown);

        var selected = shown.SelectedError;
        if (selected != null)
        {
            using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center }))
            {
                ui.Glyph(ui.State.Icons.StatusError, theme.Danger, theme.IconSize);
                ui.Text(GetText(shown, selected, t).Title, new Box { Width = Sizing.Grow() }, bold: true, overflow: TextOverflow.Ellipsis);
            }
        }

        using (ui.Panel(new Box { Height = Sizing.Grow(), Padding = theme.BorderWidth, Background = theme.EditorBackground, Clip = true }))
            CodeViewWidget.RenderCodeView(ui, "error-text", GetSelectedDocument(shown, t), shown.View);

        using (ui.Row(new Box { Gap = theme.GapTight }))
        {
            if (ui.Button(t.ErrorsCopy, id: "error-copy") && selected != null)
                ui.ClipboardText = GetText(shown, selected, t).FullText;
            ui.Tooltip(ui.GetId("error-copy"), t.ErrorsCopyTooltip);
            if (ui.Button(t.ErrorsClear, id: "error-clear", variant: ButtonVariant.Ghost))
                Commands.Later(ui.State, shown, static (a, s) => ClearErrors(a.Errors, s));
            ui.Spacer();
            if (ui.Button(t.ErrorsClose, id: "error-close", variant: ButtonVariant.Primary))
                shown.IsOpen = false;
        }
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
                ui.Text(GetText(shown, entry, t).Title, new Box { Width = Sizing.Grow() }, color: titleColor, overflow: TextOverflow.Ellipsis);
            }
        }
        ItemSelections.TakeFrom(shown.Selection, entries);
    }
}
