using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.CommandArea;
using ILSight.Core;
using ILSight.ExportArea;

namespace ILSight.MainArea;

/// <summary>The shown node's full name at the left, ellipsized, and a content-sized cluster at the
/// right: a refused request and why, activity, errors, language and the assembly count.</summary>
internal static class StatusBarView
{
    private const int RefusalDetailsSalt = 0x52454644;

    public static void Render(Ui ui, Appearance appearance, IAppCommandWriter commands, Panels panels, BrowseTree tree, ErrorDialog errorDialog, RefusalDetails refusalDetails, AppState appState)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var size = theme.FontSizeSmall;
        using (ui.Row(new Box
        {
            Height = theme.StatusBarHeight,
            Padding = new Spacing(theme.ShellPaddingX, 0f),
            Gap = theme.GroupGap,
            AlignY = Align.Center,
            Background = theme.Surface,
        }))
        {
            var selected = appState.ListSession.Navigation.ActiveTab?.Node;
            ui.Text(ui.Memo("status-node", (selected, t), static a => a.selected is null ? "" : NodeText.FullName(a.t, a.selected)), new Box { Width = Sizing.Grow() },
                color: theme.TextMuted,
                size: size, overflow: TextOverflow.Ellipsis);

            // Shrinkable, as the refusal note in it is: in a narrow window the note's why gives up room, so what follows
            // it, the error indicator among them, stays on the bar.
            using (ui.Row(new Box { Width = Sizing.Shrink(), Height = Sizing.Grow(), Gap = theme.GapTight, AlignY = Align.Center }))
            {
                if (ExportStatusView.Render(ui, appearance, commands, panels, appState.Export))
                {
                    Dot(ui);
                }

                var refusal = appState.Refusals.Shown;
                if (refusal is not null)
                {
                    RefusalNote(ui, appearance, refusalDetails, commands, refusal);
                    Dot(ui);
                }

                if (appState.Updates.Status is UpdateStatus.Ready)
                {
                    RestartToUpdateButton(ui, appearance, commands, appState.Updates);
                    Dot(ui);
                }
                var activity = DescribeActivity(appState, t);
                if (activity is not null)
                {
                    ui.Text(activity, color: theme.AccentText, size: size);
                    Dot(ui);
                }
                if (appState.Errors.Entries.Count > 0)
                {
                    ErrorIndicator(ui, appearance, errorDialog, appState.Errors);
                    Dot(ui);
                }
                ui.Text(DecompilationLanguages.Label(appState.Decompiler.Setup.Language), color: theme.TextMuted, size: size);
                Dot(ui);
                ui.Text(ui.Memo("status-count", (tree.Roots.Count, t), static a => a.t.StatusAssemblies(a.Count)), color: theme.TextMuted, size: size);
            }
        }
    }

    private static string? DescribeActivity(AppState appState, Strings t)
    {
        if (appState.ListSession.Reload.IsRunning)
        {
            return t.StatusReloading;
        }

        if (appState.Reopens.IsRunning)
        {
            return t.StatusReloading;
        }

        if (appState.ListSession.Navigation.IsOpening)
        {
            return t.StatusOpening;
        }

        if (IsDecompiling(appState.ListSession.Navigation.ActiveTab))
        {
            return t.StatusDecompiling;
        }

        if (appState.ListSession.Navigation.IsResolving)
        {
            return t.StatusResolving;
        }

        return null;
    }

    private static bool IsDecompiling(DocumentTab? tab)
    {
        return tab is not null && tab.Load.IsBusy;
    }

    private static void Dot(Ui ui)
    {
        ui.Text("·", color: ui.Theme.TextDisabled, size: ui.Theme.FontSizeSmall);
    }

    private static void RestartToUpdateButton(Ui ui, Appearance appearance, IAppCommandWriter commands, AppUpdates updates)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var i = ui.Clickable("restart-to-update");
        if (i.Clicked)
        {
            AboutDialogView.Restart(ui, commands, updates.StartRestart());
        }

        using (ui.Row(i.Id, new Box
        {
            Width = Sizing.Fit(),
            Height = Sizing.Grow(),
            Padding = new Spacing(theme.GapTight, 0f),
            AlignY = Align.Center,
            Background = i.Held ? theme.Pressed(theme.Surface) : i.Hovered ? theme.RowHover : 0u,
        }))
        {
            ui.Text(t.StatusRestartToUpdate, color: theme.AccentText, size: theme.FontSizeSmall);
        }
    }

    private static void RefusalNote(Ui ui, Appearance appearance, RefusalDetails refusalDetails, IAppCommandWriter commands, Refusal refusal)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var opened = refusalDetails;
        opened.CloseUnlessFor(refusal);
        var items = refusal.ListItems();
        // A Tab stop, so the note is reached as the line is, by the keyboard too: Enter (or Space) opens its whole list.
        var i = ui.Pressable(ui.GetId("refusal"), enabled: true, out var activated, out var ring);
        if (i.Clicked)
        {
            Commands.Later(commands, static a => a.Refusals.Dismiss());
        }

        if (activated && items is not null)
        {
            opened.Open(refusal);
        }

        var what = ui.Memo("status-refused", (refusal, t), static a => a.refusal.Words(a.t).What);
        var why = ui.Memo("status-refused-why", (refusal, t), static a => a.refusal.Words(a.t).Why);
        // The line shows what fits in the room it is drawn in; all of it is in the note's details, opened from the
        // keyboard, and in its tooltip for the pointer, beside how to dismiss it.
        var tooltip = ui.Memo("status-refused-details", (refusal, t), static a => DescribeRefusal(a.refusal, a.t));
        using (ui.Row(i.Id, new Box
        {
            Width = Sizing.Shrink(),
            Height = Sizing.Grow(),
            Gap = theme.GapTight,
            Padding = new Spacing(theme.GapTight, 0f),
            AlignY = Align.Center,
            Background = i.Held ? theme.Pressed(theme.Surface) : i.Hovered ? theme.RowHover : 0u,
        }))
        {
            if (ring)
            {
                ui.FocusRing(0f);
            }

            ui.Text(what, color: theme.Warning, size: theme.FontSizeSmall);
            ui.Text(why, new Box { Width = Sizing.Shrink() }, color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
        }
        ui.Semantics(i.Id, SemanticRole.Button, $"{what} {why}", value: items is null ? null : t.StatusRefusalDetails);
        ui.Tooltip(i.Id, tooltip);
        RefusalDetailsFlyout(ui, opened, i.Id, items);
    }

    // The list, an item a line, below the note (above it, the bar being at the window's foot); Escape or a press
    // elsewhere closes it.
    private static void RefusalDetailsFlyout(Ui ui, RefusalDetails opened, int noteId, IReadOnlyList<string>? items)
    {
        if (items is null)
        {
            opened.IsOpen = false;
            return;
        }

        var theme = ui.Theme;
        using var flyout = ui.Flyout(HashCode.Combine(noteId, RefusalDetailsSalt), ref opened.IsOpen, noteId);
        if (!flyout.IsOpen)
        {
            return;
        }

        foreach (var item in items)
        {
            ui.Text(item, color: theme.Text, size: theme.FontSizeSmall);
        }
    }

    private static string DescribeRefusal(Refusal refusal, Strings t)
    {
        var items = refusal.ListItems();
        if (items is null)
        {
            return t.StatusDismissRefusal;
        }

        return $"{refusal.Words(t).Why}\n{t.StatusDismissRefusal}";
    }

    private static void ErrorIndicator(Ui ui, Appearance appearance, ErrorDialog errorDialog, ErrorLog errors)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var i = ui.Clickable("errors");
        if (i.Clicked)
        {
            errorDialog.Open(errors.Entries);
        }

        using (ui.Row(i.Id, new Box
        {
            Width = Sizing.Fit(),
            Height = Sizing.Grow(),
            Gap = theme.GapTight,
            Padding = new Spacing(theme.GapTight, 0f),
            AlignY = Align.Center,
            Background = i.Held ? theme.Pressed(theme.DangerSurface) : i.Hovered ? theme.DangerSurface : 0u,
        }))
        {
            ui.Glyph(appearance.Icons.StatusError, theme.Danger, theme.IconSize);
            ui.Text(ui.Memo("status-errors", (errors.Entries.Count, t), static a => a.t.StatusErrors(a.Count)), color: theme.Danger,
                size: theme.FontSizeSmall);
        }
        ui.Tooltip(i.Id, errorDialog.Texts.GetText(errors.Entries[^1], t).Summary);
    }
}
