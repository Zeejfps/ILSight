using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.CommandArea;

namespace ILSight.BookmarkArea;

/// <summary>Asks whether imported bookmarks replace the ones there are or merge in beside them, one
/// import at a time.</summary>
internal static class BookmarkImportDialogView
{
    public static void Render(Ui ui, Appearance appearance, IAppCommandWriter commands, BookmarkImports imports)
    {
        var import = imports.Asking;
        if (import is null)
        {
            return;
        }

        Ask(ui, appearance, commands, import);
    }

    private static void Ask(Ui ui, Appearance appearance, IAppCommandWriter commands, PendingImport import)
    {
        var t = appearance.Strings;
        var open = true;
        using (var dialog = ui.Dialog("bookmark-import", t.BookmarksImportTitle, ref open, closeButton: false))
        {
            if (dialog.IsOpen)
            {
                ui.Text(t.BookmarksImportQuestion, wrap: true);
                using (ui.DialogActions())
                {
                    if (ui.Button(t.BookmarksImportCancel, id: "import-cancel", variant: ButtonVariant.Ghost))
                    {
                        Commands.Later(commands, import, static (a, i) => a.BookmarkImporter.FinishImport(a.Bookmarks, a.Imports, i, null));
                    }

                    if (ui.Button(t.BookmarksImportReplace, id: "import-replace"))
                    {
                        Commands.Later(commands, import, static (a, i) => a.BookmarkImporter.FinishImport(a.Bookmarks, a.Imports, i, BookmarkImportMode.Replace));
                    }

                    if (ui.Button(t.BookmarksImportMerge, id: "import-merge", variant: ButtonVariant.Primary))
                    {
                        Commands.Later(commands, import, static (a, i) => a.BookmarkImporter.FinishImport(a.Bookmarks, a.Imports, i, BookmarkImportMode.Merge));
                    }
                }
            }
        }
        if (!open)
        {
            Commands.Later(commands, import, static (a, i) => a.BookmarkImporter.FinishImport(a.Bookmarks, a.Imports, i, null));
        }
    }
}
