using System.Runtime.CompilerServices;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.CodeViewArea;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;

namespace ILSight.BookmarkArea;

internal static class BookmarksPanelView
{
    /// <summary>The bookmark list's focus and scroll id.</summary>
    public const int ListId = 0x424D524B;

    private const string MenuId = "bookmark-menu";

    private const string SuggestedExportName = "ILSight.Bookmarks.json";

    private const int MarkColumn = 0;
    internal const int NameColumn = 1;
    private const int LocationColumn = 2;
    private const int ModuleColumn = 3;

    private static readonly ConditionalWeakTable<Strings, string[]> HeaderTexts = new();

    public static void Render(Ui<UiState> ui, BookmarkList bookmarks, AssemblyList assemblies)
    {
        var t = ui.State.Strings;
        var panel = ui.State.BookmarksPanel;
        panel.Renaming.FollowList(bookmarks.Items);
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        Toolbar(ui, bookmarks);
        ui.Separator();

        if (bookmarks.Items.Count == 0)
        {
            DocumentsView.Hint(ui, t.BookmarksHint);
            return;
        }

        ItemSelections.ShowIn(panel.Selection, bookmarks.Items);
        var table = ui.Table(ListId, ui.State.BookmarkColumns, panel.Cells);
        table.Header(HeaderTexts.GetValue(t, static t => ListHeaders(t)));
        var removed = List(ui, bookmarks, assemblies, table);
        Menu(ui);
        if (removed is not null)
        {
            Commands.Later(ui.State, (panel, removed), static (a, s) => Remove(s.panel, a.Bookmarks, s.removed));
        }
    }

    private static Bookmark? List(Ui<UiState> ui, BookmarkList bookmarks, AssemblyList assemblies, TableFrame table)
    {
        var panel = ui.State.BookmarksPanel;
        var palette = ui.State.CodeViewPalette;
        var renaming = panel.Renaming.Bookmark is not null;
        Bookmark? removed = null;
        var selected = panel.Selected;
        if (table.Focused && !renaming && selected is not null && ui.Input.TakeKey(KeyboardKey.F2))
        {
            RenameInPlace(panel, selected);
        }

        using var body = table.Body(bookmarks.Items.Count, keys: !renaming);
        ItemSelections.TakeFrom(panel.Selection, bookmarks.Items);
        if (body.Focused && !renaming)
        {
            removed = HandleKeys(ui, panel);
        }

        for (var i = body.First; i < body.End; i++)
        {
            Row(ui, assemblies, body, bookmarks.Items[i], i, palette);
        }

        ItemSelections.TakeFrom(panel.Selection, bookmarks.Items);
        Answer(ui, body);
        return removed;
    }

    private static void Answer(Ui<UiState> ui, TableBody body)
    {
        var panel = ui.State.BookmarksPanel;
        var selected = panel.Selected;
        if (selected is null)
        {
            return;
        }

        if (body.Activated && panel.Renaming.Bookmark is null)
        {
            Commands.Start(ui.State, nameof(GoToBookmarkHandler), selected, static (a, b, ct) => a.GoToBookmarkHandler.HandleAsync(a, b, ct));
        }

        if (body.EditRequested && panel.Cells.Column == NameColumn)
        {
            panel.StartRename(selected);
        }

        if (body.CopyRequested && panel.Cells.HasCell)
        {
            ui.ClipboardText = Copied(selected, panel.Cells);
        }

        if (body.ContextMenuRequested)
        {
            ui.OpenContextMenu(MenuId, body.ContextMenuAt);
        }
    }

    private static string[] ListHeaders(Strings t)
    {
        return ["", t.BookmarksColumnName, t.BookmarksColumnLocation, t.BookmarksColumnModule];
    }

    private static FileFilter[] Filters(Strings t)
    {
        return [
        new FileFilter(t.BookmarksFileFilter, ["*.json"]),
        new FileFilter(t.FileFilterAll, ["*.*"]),
    ];
    }

    private static void Toolbar(Ui<UiState> ui, BookmarkList bookmarks)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var panel = ui.State.BookmarksPanel;
        var any = bookmarks.HasEnabled();
        var selected = panel.Selected;
        using (ui.PaneToolbar())
        {
            if (ui.IconButton("previous", ui.State.Icons.BookmarksPrevious, t.BookmarksPrevious, any))
            {
                Commands.Start(ui.State, nameof(StepAsync), panel, static (a, p, ct) => StepAsync(p, a, -1, ct));
            }

            if (ui.IconButton("next", ui.State.Icons.BookmarksNext, t.BookmarksNext, any))
            {
                Commands.Start(ui.State, nameof(StepAsync), panel, static (a, p, ct) => StepAsync(p, a, 1, ct));
            }

            ui.Separator(theme.IconSize);
            if (ui.IconButton("delete", ui.State.Icons.BookmarksDelete, t.BookmarksDelete, selected is not null) && selected is not null)
            {
                Commands.Later(ui.State, (panel, selected), static (a, s) => Remove(s.panel, a.Bookmarks, s.selected));
            }

            ui.Separator(theme.IconSize);
            if (ui.IconButton("export", ui.State.Icons.BookmarksExport, t.BookmarksExport, bookmarks.Items.Count > 0))
            {
                PickExportFile(ui);
            }

            if (ui.IconButton("import", ui.State.Icons.BookmarksImport, t.BookmarksImport))
            {
                PickImportFile(ui);
            }
        }
    }

    private static void PickExportFile(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var screen = ui.State;
        ui.PickSaveFile(t.BookmarksExportTitle, null, SuggestedExportName, Filters(t), path => Commands.Later(screen, path, static (a, p) => a.ExportBookmarksHandler.Handle(a.Bookmarks, p)));
    }

    private static void PickImportFile(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var screen = ui.State;
        ui.PickFile(t.BookmarksImportTitle, null, Filters(t), path => Commands.Start(screen, nameof(BookmarkImporting.ImportAsync), path, static (a, p, ct) => a.BookmarkImporting.ImportAsync(a.Bookmarks, a.Imports, p, ct)));
    }

    private static string Shown(Bookmark bookmark, int column)
    {
        return column switch
        {
            NameColumn => bookmark.Name,
            LocationColumn => bookmark.Location,
            ModuleColumn => bookmark.Module,
            _ => "",
        };
    }

    private static void Row(Ui<UiState> ui, AssemblyList assemblies, TableBody body, Bookmark bookmark, int index, CodeViewPalette palette)
    {
        var panel = ui.State.BookmarksPanel;
        var listed = assemblies.IsListed(bookmark.Place.FilePath);
        var row = body.Row(index, RuntimeHelpers.GetHashCode(bookmark));
        int locationId;
        int moduleId;
        using (row)
        {
            MarkCell(ui, row, bookmark, palette);
            NameCell(ui, panel, row, bookmark, listed);
            LocationCell(ui, row, bookmark, listed);
            TextCell(ui, row, bookmark, listed, ModuleColumn);
            locationId = row.CellAt(LocationColumn).Id;
            moduleId = row.CellAt(ModuleColumn).Id;
        }
        LocationTooltip(ui, locationId, bookmark, listed);
        ModuleTooltip(ui, moduleId, bookmark, listed);
    }

    private static bool IsDimmed(Bookmark bookmark, bool listed)
    {
        return !bookmark.Enabled || !listed;
    }

    private static void MarkCell(Ui<UiState> ui, TableRow row, Bookmark bookmark, CodeViewPalette palette)
    {
        var t = ui.State.Strings;
        var mark = row.CellAt(MarkColumn);
        if (mark.Hovered)
        {
            ui.SetCursor(MouseCursor.Hand);
        }

        if (mark.Clicked)
        {
            Commands.Later(ui.State, bookmark, static (a, b) => ToggleEnabled(a, b));
        }

        var color = bookmark.Enabled ? palette.Bookmark : palette.GutterText;
        using (row.Cell(MarkColumn))
        {
            ui.Icon(new UiIcon(ui.State.Icons.BookmarksMark, color));
        }

        ui.Tooltip(mark.Id, bookmark.Enabled ? t.BookmarksDisable : t.BookmarksEnable);
    }

    private static void NameCell(Ui<UiState> ui, BookmarksPanel panel, TableRow row, Bookmark bookmark, bool listed)
    {
        if (panel.Renaming.Bookmark != bookmark)
        {
            TextCell(ui, row, bookmark, listed, NameColumn);
            return;
        }
        using (row.Cell(NameColumn))
        {
            RenameField(ui, panel);
        }
    }

    private static void LocationCell(Ui<UiState> ui, TableRow row, Bookmark bookmark, bool listed)
    {
        var t = ui.State.Strings;
        uint? color = null;
        if (IsDimmed(bookmark, listed))
        {
            color = ui.Theme.TextMuted;
        }

        var followed = row.TextCell(LocationColumn, bookmark.Location, color, link: new UiIcon(ui.State.Icons.LinkBookmark), linkTooltip: t.BookmarksGoTo);
        if (followed)
        {
            Commands.Start(ui.State, nameof(GoToBookmarkHandler), bookmark, static (a, b, ct) => a.GoToBookmarkHandler.HandleAsync(a, b, ct));
        }
    }

    private static void TextCell(Ui<UiState> ui, TableRow row, Bookmark bookmark, bool listed, int column)
    {
        var theme = ui.Theme;
        var module = column == ModuleColumn;
        var color = theme.Text;
        if (module || IsDimmed(bookmark, listed))
        {
            color = theme.TextMuted;
        }

        var overflow = module ? TextOverflow.EllipsisMiddle : TextOverflow.Ellipsis;
        row.TextCell(column, Shown(bookmark, column), color, overflow: overflow);
    }

    private static void LocationTooltip(Ui<UiState> ui, int cellId, Bookmark bookmark, bool listed)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using var tooltip = ui.TooltipContent(cellId);
        if (!tooltip.IsOpen)
        {
            return;
        }

        using (ui.Column(new Box { Gap = theme.GapTight }))
        {
            ui.Text(bookmark.Place.ToString(), color: theme.Text, size: theme.FontSizeSmall, wrap: true);
            if (!listed)
            {
                NotListedText(ui, bookmark);
            }

            ui.Text(t.BookmarksGoToHint, color: theme.TextMuted, size: theme.FontSizeSmall, wrap: true);
        }
    }

    private static void ModuleTooltip(Ui<UiState> ui, int cellId, Bookmark bookmark, bool listed)
    {
        if (listed)
        {
            return;
        }

        using var tooltip = ui.TooltipContent(cellId);
        if (!tooltip.IsOpen)
        {
            return;
        }

        NotListedText(ui, bookmark);
    }

    private static void NotListedText(Ui<UiState> ui, Bookmark bookmark)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        ui.Text(t.BookmarksNotListed(bookmark.Module), color: theme.Text, size: theme.FontSizeSmall, wrap: true);
    }

    private static void RenameField(Ui<UiState> ui, BookmarksPanel panel)
    {
        var begin = panel.Renaming.TryTakeFocus();
        var result = ui.InlineEdit("name", ref panel.RenameText, begin, Sizing.Grow());
        if (result == InlineEditResult.Editing)
        {
            return;
        }

        var stillHere = ui.HasFocus(ui.GetId("name"));
        if (result == InlineEditResult.Committed)
        {
            CommitRename(ui.State, panel);
        }
        else
        {
            panel.Renaming.End();
        }

        panel.Cells.StopEditing();
        if (stillHere || result == InlineEditResult.Cancelled)
        {
            ui.Focus(ListId, takesText: false);
        }
    }

    private static void Menu(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var panel = ui.State.BookmarksPanel;
        var bookmark = panel.Selected;
        if (bookmark is null)
        {
            return;
        }

        if (ui.MenuItem(t.BookmarksGoTo, Shortcuts.Enter, id: "go-to"))
        {
            Commands.Start(ui.State, nameof(GoToBookmarkHandler), bookmark, static (a, b, ct) => a.GoToBookmarkHandler.HandleAsync(a, b, ct));
        }

        if (ui.MenuItem(t.CodeCopy, Shortcuts.Copy, enabled: panel.Cells.HasCell, id: "copy"))
        {
            ui.ClipboardText = Copied(bookmark, panel.Cells);
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.BookmarksRename, Shortcuts.Rename, id: "rename"))
        {
            RenameInPlace(panel, bookmark);
        }

        if (ui.MenuItem(bookmark.Enabled ? t.BookmarksDisable : t.BookmarksEnable, Shortcuts.Space, id: "enable"))
        {
            Commands.Later(ui.State, bookmark, static (a, b) => ToggleEnabled(a, b));
        }

        if (ui.MenuItem(t.BookmarksDelete, Shortcuts.Delete, id: "delete"))
        {
            Commands.Later(ui.State, (panel, bookmark), static (a, s) => Remove(s.panel, a.Bookmarks, s.bookmark));
        }
    }

    private static string Copied(Bookmark bookmark, TableCellSelection cells)
    {
        var shown = Shown(bookmark, cells.Column);
        return cells.Copied(shown, shown);
    }

    private static Bookmark? HandleKeys(Ui<UiState> ui, BookmarksPanel panel)
    {
        Bookmark? removed = null;
        foreach (var stroke in ui.Input.Keys)
        {
            var bookmark = panel.Selected;
            if (bookmark is null)
            {
                break;
            }

            if (HasCommandModifier(stroke))
            {
                continue;
            }

            switch (stroke.Key)
            {
                case KeyboardKey.Space:
                    Commands.Later(ui.State, bookmark, static (a, b) => ToggleEnabled(a, b));
                    break;
                case KeyboardKey.Delete:
                    removed = bookmark;
                    break;
            }
        }
        return removed;
    }

    private static bool HasCommandModifier(KeyStroke stroke)
    {
        return (stroke.Modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Super)) != 0;
    }

    /// <summary>Removes <paramref name="bookmark"/>, passing its selection to the bookmark now in its row; nothing
    /// when it is no longer listed.</summary>
    internal static void Remove(BookmarksPanel panel, BookmarkList bookmarks, Bookmark bookmark)
    {
        var row = bookmarks.Remove(bookmark);
        if (row is null)
        {
            return;
        }

        panel.TakeRemoved(bookmark, row.Value, bookmarks.Items);
    }

    /// <summary>Selects the next enabled bookmark after the selected one, or the previous when
    /// <paramref name="direction"/> is negative, and goes to it (<see cref="GoToBookmarkHandler"/>), returning the load
    /// that started in the tab; null when none is enabled.</summary>
    internal static Task<DecompileTicket?> StepAsync(BookmarksPanel panel, AppState appState, int direction, CancellationToken ct)
    {
        var bookmark = appState.Bookmarks.FindNext(panel.Selected, direction);
        if (bookmark is null)
        {
            return Task.FromResult<DecompileTicket?>(null);
        }

        ItemSelections.Select(panel.Selection, bookmark, appState.Bookmarks.Items);
        return appState.GoToBookmarkHandler.HandleAsync(appState, bookmark, ct);
    }

    private static void RenameInPlace(BookmarksPanel panel, Bookmark bookmark)
    {
        panel.Cells.SelectColumn(NameColumn);
        panel.Cells.Edit();
        panel.StartRename(bookmark);
    }

    private static void CommitRename(UiState screen, BookmarksPanel panel)
    {
        var bookmark = panel.Renaming.End();
        if (bookmark is null)
        {
            return;
        }

        Commands.Later(screen, (bookmark, name: panel.RenameText), static (a, s) => a.Bookmarks.Rename(s.bookmark, s.name));
    }

    private static void ToggleEnabled(AppState appState, Bookmark bookmark)
    {
        appState.Bookmarks.SetEnabled(bookmark, !bookmark.Enabled);
    }
}
