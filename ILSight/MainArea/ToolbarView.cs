using System.Runtime.CompilerServices;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CommandArea;
using ILSight.AssemblyListArea;

namespace ILSight.MainArea;

internal static class ToolbarView
{
    private const string ListItemPrefix = "list:";
    private const string ILWithCSharpLabel = "IL+C#";


    private static readonly ConditionalWeakTable<Strings, Segment<DecompilationLanguage>[]> LanguageSegments = new();

    /// <summary>The toolbar, over the parts its controls read and change: the saved lists and the open list's session
    /// (the list picker, history), how the screen is laid out (switching lists keeps it), the exports finished (history
    /// shows their reports again) and the decompiler's choice (the language, its version, the history's loads).</summary>
    public static void Render(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view,
        IReadOnlyList<ExportArea.ExportReport> finishedExports, DecompilerChoice decompiler)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.Row(new Box
        {
            Height = theme.ToolbarHeight,
            Padding = new Spacing(theme.ShellPaddingX, theme.ToolbarPaddingY),
            Gap = theme.GroupGap,
            AlignY = Align.Center,
            Background = theme.Surface,
        }))
        {
            using (Group(ui))
            {
                ListPicker(ui, lists, session, view);
                if (ui.IconButton("manage-lists", ui.State.Icons.ToolbarManageLists, t.ListsManage, variant: ButtonVariant.Neutral))
                {
                    Commands.Later(ui.State, ui.State.ListsDialog, static (a, dialog) => AssemblyListsDialogView.Open(dialog, a));
                }
            }

            using (Group(ui))
            {
                var openTip = ui.Memo("open-tip", t, static s => s.ToolbarOpen(Shortcuts.Open));
                if (ui.IconButton("open", ui.State.Icons.ToolbarOpen, openTip, variant: ButtonVariant.Neutral))
                {
                    MainMenuView.OpenFile(ui, session.Navigation);
                }

                var searchTip = ui.Memo("search-tip", t, static s => s.SearchFieldTooltip(Shortcuts.Search));
                if (ui.IconButton("search", ui.State.Icons.ToolbarSearch, searchTip, variant: ButtonVariant.Neutral))
                {
                    ScreenLayout.OpenSearch(ui.State);
                }
            }

            using (Group(ui))
            {
                HistoryButtons(ui, session, finishedExports, decompiler.Setup);
            }

            using (Group(ui))
            {
                var language = decompiler.Setup.Language;
                var languages = Languages(t);
                if (ui.SegmentedControl("language", ref language, languages))
                {
                    DocumentPanels.ShowInLanguage(ui.State, decompiler, language);
                }

                if (DecompilationLanguages.ShowsCSharp(language))
                {
                    LanguageVersion(ui, decompiler.Setup.Options);
                    if (ui.IconButton("decompiler-options", ui.State.Icons.ToolbarDecompilerOptions, t.PanelDecompilerOptions, variant: ButtonVariant.Neutral))
                    {
                        ScreenLayout.OpenPanel(ui.State, DockView.DecompilerOptions);
                    }
                }
            }

            // The gear is on its own at the far end, where settings usually are, and shows whatever the
            // language: display options apply to IL too.
            ui.Leaf(new Box { Width = Sizing.Grow(), Height = 0f });
            var displayOptionsTip = ui.Memo("display-options-tip", t, static s => s.ToolbarDisplayOptions(Shortcuts.Settings));
            if (ui.IconButton("display-options", ui.State.Icons.ToolbarDisplayOptions, displayOptionsTip, variant: ButtonVariant.Neutral))
            {
                ScreenLayout.OpenPanel(ui.State, DockView.DisplayOptions);
            }
        }
    }

    private static void HistoryButtons(Ui<UiState> ui, OpenListSession session, IReadOnlyList<ExportArea.ExportReport> finishedExports, DecompilerSetup setup)
    {
        var t = ui.State.Strings;
        var navigation = session.Navigation;
        using (var back = ui.SplitIconButton("back", ui.State.Icons.ToolbarBack,
                   ui.Memo("back-tip", t, static s => s.ToolbarBack(Shortcuts.Back)), t.ToolbarBackHistory, navigation.CanGoBack, navigation.HistoryRevision))
        {
            if (back.Clicked)
            {
                Commands.MoveLater(ui.State, navigation.GoBack(session.Assemblies, finishedExports, setup));
            }

            if (back.IsOpen)
            {
                HistoryItems(ui, navigation, session.Assemblies, finishedExports, setup, -1);
            }
        }
        using (var forward = ui.SplitIconButton("forward", ui.State.Icons.ToolbarForward,
                   ui.Memo("forward-tip", t, static s => s.ToolbarForward(Shortcuts.Forward)), t.ToolbarForwardHistory, navigation.CanGoForward,
                   navigation.HistoryRevision))
        {
            if (forward.Clicked)
            {
                Commands.MoveLater(ui.State, navigation.GoForward(session.Assemblies, finishedExports, setup));
            }

            if (forward.IsOpen)
            {
                HistoryItems(ui, navigation, session.Assemblies, finishedExports, setup, 1);
            }
        }
    }

    private static void ListPicker(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view)
    {
        var t = ui.State.Strings;
        var width = ListPickerWidth(ui, lists);
        using var menu = ui.SelectButton("list", lists.Active, width, t.ListsPick, revision: lists.Count);
        if (!menu.IsOpen)
        {
            return;
        }

        var names = lists.Names;
        for (var i = 0; i < names.Count; i++)
        {
            ListItem(ui, lists, session, view, names[i]);
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.ListsManage, id: "manage", icon: new UiIcon(ui.State.Icons.ListsManage)))
        {
            Commands.Later(ui.State, ui.State.ListsDialog, static (a, dialog) => AssemblyListsDialogView.Open(dialog, a));
        }
    }

    // Measured again only when the lists or the theme changed, so a frame allocates nothing.
    private static float ListPickerWidth(Ui<UiState> ui, SavedAssemblyLists lists)
    {
        var measured = ui.GetState<MeasuredWidth>(ui.GetId("list-width"));
        var unchanged = lists.Revision == measured.Revision && ReferenceEquals(ui.Theme, measured.Theme);
        if (unchanged)
        {
            return measured.Width;
        }

        measured.Revision = lists.Revision;
        measured.Theme = ui.Theme;
        var names = lists.Names.ToArray();
        var widest = ui.SelectButtonWidth(names);
        measured.Width = Math.Clamp(widest, AppLayout.ListPickerMinWidth, AppLayout.ListPickerMaxWidth);
        return measured.Width;
    }

    private sealed class MeasuredWidth
    {
        public int Revision = -1;
        public UiTheme? Theme;
        public float Width;
    }

    private static void ListItem(Ui<UiState> ui, SavedAssemblyLists lists, OpenListSession session, ViewSettings view, string name)
    {
        var active = name == lists.Active;
        if (!ui.MenuRadioItem(name, active, id: ListItemPrefix + name))
        {
            return;
        }

        Commands.OpenListLater(ui.State, AssemblyLists.SwitchAssemblyList(lists, session, view, name));
        // The picker, drawn above this frame, shows the list only in the next one.
        ui.RequestUpdate();
    }

    private static Segment<DecompilationLanguage>[] Languages(Strings t)
    {
        return LanguageSegments.GetValue(t, static t =>
    [
        new Segment<DecompilationLanguage>(DecompilationLanguage.CSharp, "C#", t.ToolbarDecompileCsharp),
        new Segment<DecompilationLanguage>(DecompilationLanguage.IL, "IL", t.ToolbarDisassembleIl),
        new Segment<DecompilationLanguage>(DecompilationLanguage.ILWithCSharp, ILWithCSharpLabel, t.ToolbarDisassembleIlWithCsharp),
    ]);
    }

    private static void LanguageVersion(Ui<UiState> ui, DecompilerOptions options)
    {
        var t = ui.State.Strings;
        var current = options.LanguageVersion;
        using var menu = ui.SelectButton("language-version", CSharpVersions.Label(current), ui.SelectButtonWidth(CSharpVersions.Labels),
            t.ToolbarLanguageVersion);
        if (!menu.IsOpen)
        {
            return;
        }

        foreach (var (version, label) in CSharpVersions.All)
        {
            if (ui.MenuRadioItem(label, version == current))
            {
                Commands.Later(ui.State, version, static (a, v) => Preferences.SetLanguageVersion(a, v));
            }
        }
    }

    private static Ui.Scope Group(Ui<UiState> ui)
    {
        return ui.Row(new Box { Width = Sizing.Fit(), Gap = ui.Theme.Gap, AlignY = Align.Center });
    }

    private static void HistoryItems(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies, IReadOnlyList<ExportArea.ExportReport> finished, DecompilerSetup setup,
        int direction)
    {
        var entries = navigation.HistoryEntries;
        for (var i = navigation.HistoryIndex + direction; i >= 0 && i < entries.Count; i += direction)
        {
            using (ui.Id(i))
            {
                var entry = entries[i];
                var text = TextOf(ui.State.Strings, entry);
                var icon = IconOf(ui.State, entry);
                if (!navigation.CanShow(entry, assemblies, finished))
                {
                    ui.MenuItem(SayWhyNotShown(ui.State.Strings, entry, text), enabled: false, id: "entry", icon: icon);
                    continue;
                }

                if (ui.MenuItem(text, id: "entry", icon: icon))
                {
                    Commands.MoveLater(ui.State, navigation.GoToEntry(i, assemblies, finished, setup));
                    return;
                }
            }
        }
    }

    private static string TextOf(Strings strings, HistoryEntry entry)
    {
        return entry.Match(strings,
            static (_, node) => node.Place.Label,
            static (_, diagram) => diagram.Title,
            static (_, comparison) => comparison.Title,
            static (strings, _) => strings.ExportReportTitle);
    }

    private static string SayWhyNotShown(Strings strings, HistoryEntry entry, string text)
    {
        return entry.Match(
            node => strings.ToolbarHistoryClosed(text, NameOf(node.Place.FilePath)),
            diagram => strings.ToolbarHistoryClosed(text, NameDiagramAssemblies(diagram)),
            comparison => strings.ToolbarHistoryClosed(text, NameComparedAssemblies(comparison)),
            report => strings.ToolbarHistoryReportRemoved(text));
    }

    private static string NameOf(AssemblyPath path)
    {
        return Path.GetFileNameWithoutExtension(path.FileName);
    }

    private static string NameDiagramAssemblies(HistoryEntry.Diagram diagram)
    {
        var paths = diagram.HomeAssemblies.Concat(diagram.Types.Select(static t => t.AssemblyPath));
        return string.Join(", ", paths.Select(NameOf).Distinct());
    }

    private static string NameComparedAssemblies(HistoryEntry.Comparison comparison)
    {
        return string.Join(", ", new[] { NameOf(comparison.LeftPath), NameOf(comparison.RightPath) }.Distinct());
    }

    private static UiIcon IconOf(UiState screen, HistoryEntry entry)
    {
        return entry.Match(screen,
            static (screen, node) => NodeIcons.Icon(node.Icon, node.Accessibility, screen.NodeIconPalette, screen.Icons),
            static (screen, _) => new UiIcon(screen.Icons.TabDiagram),
            static (screen, _) => new UiIcon(screen.Icons.TabPage),
            static (screen, _) => new UiIcon(screen.Icons.TabPage));
    }
}
