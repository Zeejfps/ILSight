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
    public static void Render(Ui<UiState> ui, AppState appState)
    {
        var t = ui.State.Appearance.Strings;
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
            ui.Text(ui.Memo("status-node", selected, static n => n is null ? "" : n.FullName), new Box { Width = Sizing.Grow() }, color: theme.TextMuted,
                size: size, overflow: TextOverflow.Ellipsis);

            // Shrinkable, as the refusal note in it is: in a narrow window the note's why gives up room, so what follows
            // it, the error indicator among them, stays on the bar.
            using (ui.Row(new Box { Width = Sizing.Shrink(), Height = Sizing.Grow(), Gap = theme.GapTight, AlignY = Align.Center }))
            {
                if (ExportStatusView.Render(ui, appState.Export))
                {
                    Dot(ui);
                }

                var refusal = appState.Refusals.Shown;
                if (refusal is not null)
                {
                    RefusalNote(ui, refusal);
                    Dot(ui);
                }

                if (appState.Updates.Status is UpdateStatus.Ready)
                {
                    RestartToUpdateButton(ui, appState.Updates);
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
                    ErrorIndicator(ui, appState.Errors);
                    Dot(ui);
                }
                ui.Text(DecompilationLanguages.Label(appState.Decompiler.Setup.Language), color: theme.TextMuted, size: size);
                Dot(ui);
                ui.Text(ui.Memo("status-count", (ui.State.Tree.Roots.Count, t), static a => a.t.StatusAssemblies(a.Count)), color: theme.TextMuted, size: size);
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

    private static void Dot(Ui<UiState> ui)
    {
        ui.Text("·", color: ui.Theme.TextDisabled, size: ui.Theme.FontSizeSmall);
    }

    private static void RestartToUpdateButton(Ui<UiState> ui, AppUpdates updates)
    {
        var t = ui.State.Appearance.Strings;
        var theme = ui.Theme;
        var i = ui.Clickable("restart-to-update");
        if (i.Clicked)
        {
            AboutDialogView.Restart(ui, updates.StartRestart());
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

    private static void RefusalNote(Ui<UiState> ui, Refusal refusal)
    {
        var t = ui.State.Appearance.Strings;
        var theme = ui.Theme;
        var i = ui.Clickable("refusal");
        if (i.Clicked)
        {
            Commands.Later(ui.State, static a => a.Refusals.Dismiss());
        }

        var what = ui.Memo("status-refused", (refusal, t), static a => a.refusal.Words(a.t).What);
        var why = ui.Memo("status-refused-why", (refusal, t), static a => a.refusal.Words(a.t).Why);
        // The line shows what fits; all of it is in the note's tooltip, beside how to dismiss it.
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
            ui.Text(what, color: theme.Warning, size: theme.FontSizeSmall);
            ui.Text(why, new Box { Width = Sizing.Shrink() }, color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
        }
        ui.Tooltip(i.Id, tooltip);
    }

    private static string DescribeRefusal(Refusal refusal, Strings t)
    {
        var details = refusal.Details(t);
        if (details is null)
        {
            return t.StatusDismissRefusal;
        }

        return $"{details}\n{t.StatusDismissRefusal}";
    }

    private static void ErrorIndicator(Ui<UiState> ui, ErrorLog errors)
    {
        var t = ui.State.Appearance.Strings;
        var theme = ui.Theme;
        var i = ui.Clickable("errors");
        if (i.Clicked)
        {
            ui.State.ErrorDialog.Open(errors.Entries);
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
            ui.Glyph(ui.State.Appearance.Icons.StatusError, theme.Danger, theme.IconSize);
            ui.Text(ui.Memo("status-errors", (errors.Entries.Count, t), static a => a.t.StatusErrors(a.Count)), color: theme.Danger,
                size: theme.FontSizeSmall);
        }
        ui.Tooltip(i.Id, ui.State.ErrorDialog.Texts.GetText(errors.Entries[^1], t).Summary);
    }
}
