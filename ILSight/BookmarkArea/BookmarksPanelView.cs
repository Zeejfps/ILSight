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

    public static void Render(Ui ui, Appearance appearance, BookmarksPanel bookmarksPanel, IAppCommandWriter commands, BookmarkList bookmarks, AssemblyList assemblies)
    {
        var t = appearance.Strings;
        var panel = bookmarksPanel;
        panel.Renaming.ForgetRenameOfRemovedBookmark(bookmarks.Items);
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        Toolbar(ui, appearance, bookmarksPanel, commands, bookmarks);
        ui.Separator();

        if (bookmarks.Items.Count == 0)
        {
            DocumentsView.Hint(ui, t.BookmarksHint);
            return;
        }

        panel.Selection.ShowIn(bookmarks.Items);
        var table = ui.Table(ListId, panel.Columns, panel.Cells);
        table.Header(HeaderTexts.GetValue(t, static t => ListHeaders(t)));
        var removed = List(ui, bookmarksPanel, appearance, commands, bookmarks, assemblies, table);
        Menu(ui, appearance, bookmarksPanel, commands);
        if (removed is not null)
        {
            Commands.Later(commands, (panel, removed), static (a, s) => Remove(s.panel, a.Bookmarks, s.removed));
        }
    }

    private static Bookmark? List(Ui ui, BookmarksPanel bookmarksPanel, Appearance appearance, IAppCommandWriter commands, BookmarkList bookmarks, AssemblyList assemblies, TableFrame table)
    {
        var panel = bookmarksPanel;
        var palette = appearance.CodeViewPalette;
        var renaming = panel.Renaming.Bookmark is not null;
        Bookmark? removed = null;
        var selected = panel.Selection.Item;
        if (table.Focused && !renaming && selected is not null && ui.Input.TakeKey(KeyboardKey.F2))
        {
            RenameInPlace(panel, selected);
        }

        using var body = table.Body(bookmarks.Items.Count, keys: !renaming);
        panel.Selection.TakeFrom(bookmarks.Items);
        if (body.Focused && !renaming)
        {
            removed = RunBookmarkShortcuts(ui, commands, panel);
        }

        for (var i = body.First; i < body.End; i++)
        {
            Row(ui, bookmarksPanel, appearance, commands, assemblies, body, bookmarks.Items[i], i, palette);
        }

        panel.Selection.TakeFrom(bookmarks.Items);
        Answer(ui, bookmarksPanel, commands, body);
        return removed;
    }

    private static void Answer(Ui ui, BookmarksPanel bookmarksPanel, IAppCommandWriter commands, TableBody body)
    {
        var panel = bookmarksPanel;
        var selected = panel.Selection.Item;
        if (selected is null)
        {
            return;
        }

        if (body.Activated && panel.Renaming.Bookmark is null)
        {
            Commands.GoToBookmarkLater(commands, selected);
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

    private static void Toolbar(Ui ui, Appearance appearance, BookmarksPanel bookmarksPanel, IAppCommandWriter commands, BookmarkList bookmarks)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var panel = bookmarksPanel;
        var any = bookmarks.HasEnabled();
        var selected = panel.Selection.Item;
        using (ui.PaneToolbar())
        {
            if (ui.IconButton("previous", appearance.Icons.BookmarksPrevious, t.BookmarksPrevious, any))
            {
                Commands.Later(commands, panel, static (a, p) => Step(p, a, -1));
            }

            if (ui.IconButton("next", appearance.Icons.BookmarksNext, t.BookmarksNext, any))
            {
                Commands.Later(commands, panel, static (a, p) => Step(p, a, 1));
            }

            ui.Separator(theme.IconSize);
            if (ui.IconButton("delete", appearance.Icons.BookmarksDelete, t.BookmarksDelete, selected is not null) && selected is not null)
            {
                Commands.Later(commands, (panel, selected), static (a, s) => Remove(s.panel, a.Bookmarks, s.selected));
            }

            ui.Separator(theme.IconSize);
            if (ui.IconButton("export", appearance.Icons.BookmarksExport, t.BookmarksExport, bookmarks.Items.Count > 0))
            {
                PickExportFile(ui, appearance, commands);
            }

            if (ui.IconButton("import", appearance.Icons.BookmarksImport, t.BookmarksImport))
            {
                PickImportFile(ui, appearance, commands);
            }
        }
    }

    private static void PickExportFile(Ui ui, Appearance appearance, IAppCommandWriter commands)
    {
        var t = appearance.Strings;
        ui.PickSaveFile(t.BookmarksExportTitle, null, SuggestedExportName, Filters(t), path => Commands.Later(commands, path, static (a, p) => a.ExportBookmarksHandler.Handle(a.Bookmarks, p)));
    }

    private static void PickImportFile(Ui ui, Appearance appearance, IAppCommandWriter commands)
    {
        var t = appearance.Strings;
        ui.PickFile(t.BookmarksImportTitle, null, Filters(t), path => Commands.Start(commands, nameof(BookmarkImporting.ImportAsync), path, static (a, p, ct) => a.BookmarkImporting.ImportAsync(a.Bookmarks, a.Imports, p, ct)));
    }

    // The cell's value as the bookmark holds it, which copying the whole cell takes; it is drawn on the row's one line
    // (OneLineText.Name), which a part selected is of.
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

    private static void Row(Ui ui, BookmarksPanel bookmarksPanel, Appearance appearance, IAppCommandWriter commands, AssemblyList assemblies, TableBody body, Bookmark bookmark, int index, CodeViewPalette palette)
    {
        var panel = bookmarksPanel;
        var listed = assemblies.IsListed(bookmark.Place.FilePath);
        var row = body.Row(index, RuntimeHelpers.GetHashCode(bookmark));
        int locationId;
        int moduleId;
        using (row)
        {
            MarkCell(ui, appearance, commands, row, bookmark, palette);
            NameCell(ui, commands, panel, row, bookmark, listed);
            LocationCell(ui, appearance, commands, row, bookmark, listed);
            TextCell(ui, row, bookmark, listed, ModuleColumn);
            locationId = row.CellAt(LocationColumn).Id;
            moduleId = row.CellAt(ModuleColumn).Id;
        }
        LocationTooltip(ui, appearance, locationId, bookmark, listed);
        ModuleTooltip(ui, appearance, moduleId, bookmark, listed);
    }

    private static bool IsDimmed(Bookmark bookmark, bool listed)
    {
        return !bookmark.Enabled || !listed;
    }

    private static void MarkCell(Ui ui, Appearance appearance, IAppCommandWriter commands, TableRow row, Bookmark bookmark, CodeViewPalette palette)
    {
        var t = appearance.Strings;
        var mark = row.CellAt(MarkColumn);
        if (mark.Hovered)
        {
            ui.SetCursor(MouseCursor.Hand);
        }

        if (mark.Clicked)
        {
            Commands.Later(commands, bookmark, static (a, b) => ToggleEnabled(a, b));
        }

        var color = bookmark.Enabled ? palette.Bookmark : palette.GutterText;
        using (row.Cell(MarkColumn))
        {
            ui.Icon(new UiIcon(appearance.Icons.BookmarksMark, color));
        }

        ui.Tooltip(mark.Id, bookmark.Enabled ? t.BookmarksDisable : t.BookmarksEnable);
    }

    private static void NameCell(Ui ui, IAppCommandWriter commands, BookmarksPanel panel, TableRow row, Bookmark bookmark, bool listed)
    {
        if (panel.Renaming.Bookmark != bookmark)
        {
            TextCell(ui, row, bookmark, listed, NameColumn);
            return;
        }
        using (row.Cell(NameColumn))
        {
            RenameField(ui, commands, panel);
        }
    }

    private static void LocationCell(Ui ui, Appearance appearance, IAppCommandWriter commands, TableRow row, Bookmark bookmark, bool listed)
    {
        var t = appearance.Strings;
        uint? color = null;
        if (IsDimmed(bookmark, listed))
        {
            color = ui.Theme.TextMuted;
        }

        var followed = row.TextCell(LocationColumn, OneLineText.Name(bookmark.Location), color, link: new UiIcon(appearance.Icons.LinkBookmark), linkTooltip: t.BookmarksGoTo);
        if (followed)
        {
            Commands.GoToBookmarkLater(commands, bookmark);
        }
    }

    private static void TextCell(Ui ui, TableRow row, Bookmark bookmark, bool listed, int column)
    {
        var theme = ui.Theme;
        var module = column == ModuleColumn;
        var color = theme.Text;
        if (module || IsDimmed(bookmark, listed))
        {
            color = theme.TextMuted;
        }

        var overflow = module ? TextOverflow.EllipsisMiddle : TextOverflow.Ellipsis;
        row.TextCell(column, OneLineText.Name(Shown(bookmark, column)), color, overflow: overflow);
    }

    private static void LocationTooltip(Ui ui, Appearance appearance, int cellId, Bookmark bookmark, bool listed)
    {
        var t = appearance.Strings;
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
                NotListedText(ui, appearance, bookmark);
            }

            ui.Text(t.BookmarksGoToHint, color: theme.TextMuted, size: theme.FontSizeSmall, wrap: true);
        }
    }

    private static void ModuleTooltip(Ui ui, Appearance appearance, int cellId, Bookmark bookmark, bool listed)
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

        NotListedText(ui, appearance, bookmark);
    }

    private static void NotListedText(Ui ui, Appearance appearance, Bookmark bookmark)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        ui.Text(t.BookmarksNotListed(bookmark.Module), color: theme.Text, size: theme.FontSizeSmall, wrap: true);
    }

    private static void RenameField(Ui ui, IAppCommandWriter commands, BookmarksPanel panel)
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
            CommitRename(commands, panel);
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

    private static void Menu(Ui ui, Appearance appearance, BookmarksPanel bookmarksPanel, IAppCommandWriter commands)
    {
        var t = appearance.Strings;
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var panel = bookmarksPanel;
        var bookmark = panel.Selection.Item;
        if (bookmark is null)
        {
            return;
        }

        if (ui.MenuItem(t.BookmarksGoTo, Shortcuts.Enter, id: "go-to"))
        {
            Commands.GoToBookmarkLater(commands, bookmark);
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
            Commands.Later(commands, bookmark, static (a, b) => ToggleEnabled(a, b));
        }

        if (ui.MenuItem(t.BookmarksDelete, Shortcuts.Delete, id: "delete"))
        {
            Commands.Later(commands, (panel, bookmark), static (a, s) => Remove(s.panel, a.Bookmarks, s.bookmark));
        }
    }

    private static string Copied(Bookmark bookmark, TableCellSelection cells)
    {
        var value = Shown(bookmark, cells.Column);
        return cells.Copied(OneLineText.Name(value), value);
    }

    private static Bookmark? RunBookmarkShortcuts(Ui ui, IAppCommandWriter commands, BookmarksPanel panel)
    {
        Bookmark? removed = null;
        foreach (var stroke in ui.Input.Keys)
        {
            var bookmark = panel.Selection.Item;
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
                    Commands.Later(commands, bookmark, static (a, b) => ToggleEnabled(a, b));
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
    /// <paramref name="direction"/> is negative, and goes to it (<see cref="Commands.StartGoingToBookmark"/>);
    /// nothing when none is enabled.</summary>
    internal static void Step(BookmarksPanel panel, AppState appState, int direction)
    {
        var bookmark = appState.Bookmarks.FindNext(panel.Selection.Item, direction);
        if (bookmark is null)
        {
            return;
        }

        panel.Selection.Select(bookmark, appState.Bookmarks.Items);
        Commands.StartGoingToBookmark(appState, bookmark);
    }

    private static void RenameInPlace(BookmarksPanel panel, Bookmark bookmark)
    {
        panel.Cells.SelectColumn(NameColumn);
        panel.Cells.Edit();
        panel.StartRename(bookmark);
    }

    private static void CommitRename(IAppCommandWriter commands, BookmarksPanel panel)
    {
        var bookmark = panel.Renaming.End();
        if (bookmark is null)
        {
            return;
        }

        Commands.Later(commands, (bookmark, name: panel.RenameText), static (a, s) => a.Bookmarks.Rename(s.bookmark, s.name));
    }

    private static void ToggleEnabled(AppState appState, Bookmark bookmark)
    {
        appState.Bookmarks.SetEnabled(bookmark, !bookmark.Enabled);
    }
}
