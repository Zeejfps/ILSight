using System.Diagnostics;
using System.Runtime.CompilerServices;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using static ILSight.AssemblyLists;
using static ILSight.Navigation;
using static ILSight.Preferences;
using static ILSight.ScreenLayout;

namespace ILSight;

internal static class ToolbarView
{
    private const string ListItemPrefix = "list:";
    private const string ILWithCSharpLabel = "IL+C#";


    private static readonly ConditionalWeakTable<Strings, Segment<DecompilationLanguage>[]> LanguageSegments = new();

    public static void Render(Ui<UiState> ui, AppState appState)
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
                ListPicker(ui, appState.Lists);
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
                    MainMenuView.OpenFile(ui);
                }

                var searchTip = ui.Memo("search-tip", t, static s => s.SearchFieldTooltip(Shortcuts.Search));
                if (ui.IconButton("search", ui.State.Icons.ToolbarSearch, searchTip, variant: ButtonVariant.Neutral))
                {
                    OpenSearch(ui.State);
                }
            }

            using (Group(ui))
            {
                using (var back = ui.SplitIconButton("back", ui.State.Icons.ToolbarBack,
                           ui.Memo("back-tip", t, static s => s.ToolbarBack(Shortcuts.Back)), t.ToolbarBackHistory, appState.History.CanGoBack, appState.History.Revision))
                {
                    if (back.Clicked)
                    {
                        Commands.Later(ui.State, static a => GoBack(a));
                    }

                    if (back.IsOpen)
                    {
                        HistoryItems(ui, appState.History, -1);
                    }
                }
                using (var forward = ui.SplitIconButton("forward", ui.State.Icons.ToolbarForward,
                           ui.Memo("forward-tip", t, static s => s.ToolbarForward(Shortcuts.Forward)), t.ToolbarForwardHistory, appState.History.CanGoForward,
                           appState.History.Revision))
                {
                    if (forward.Clicked)
                    {
                        Commands.Later(ui.State, static a => GoForward(a));
                    }

                    if (forward.IsOpen)
                    {
                        HistoryItems(ui, appState.History, 1);
                    }
                }
            }

            using (Group(ui))
            {
                var language = appState.Language;
                var languages = Languages(t);
                if (ui.SegmentedControl("language", ref language, languages))
                {
                    Commands.Later(ui.State, language, static (a, l) => a.Language = l);
                }

                if (DecompilationLanguages.ShowsCSharp(language))
                {
                    LanguageVersion(ui, appState.DecompilerOptions);
                    if (ui.IconButton("decompiler-options", ui.State.Icons.ToolbarDecompilerOptions, t.PanelDecompilerOptions, variant: ButtonVariant.Neutral))
                    {
                        OpenPanel(ui.State, DockView.DecompilerOptions);
                    }
                }
            }

            // The gear is on its own at the far end, where settings usually are, and shows whatever the
            // language: display options apply to IL too.
            ui.Leaf(new Box { Width = Sizing.Grow(), Height = 0f });
            var displayOptionsTip = ui.Memo("display-options-tip", t, static s => s.ToolbarDisplayOptions(Shortcuts.Settings));
            if (ui.IconButton("display-options", ui.State.Icons.ToolbarDisplayOptions, displayOptionsTip, variant: ButtonVariant.Neutral))
            {
                OpenPanel(ui.State, DockView.DisplayOptions);
            }
        }
    }

    /// <summary>The open assembly list's name, opening a menu of every list to switch to, and of the
    /// dialog that manages them. As wide as the longest name, within limits.</summary>
    private static void ListPicker(Ui<UiState> ui, SavedAssemblyLists lists)
    {
        var t = ui.State.Strings;
        var width = ListPickerWidth(ui, lists);
        using var menu = ui.SelectButton("list", lists.Active, width, t.ListsPick, revision: lists.Count);
        if (!menu.IsOpen)
        {
            return;
        }

        foreach (var name in lists.Names)
        {
            ListItem(ui, lists, name);
        }

        ui.MenuSeparator();
        if (ui.MenuItem(t.ListsManage, id: "manage", icon: new UiIcon(ui.State.Icons.ListsManage)))
        {
            Commands.Later(ui.State, ui.State.ListsDialog, static (a, dialog) => AssemblyListsDialogView.Open(dialog, a));
        }
    }

    /// <summary>Measured again only when the lists or the theme changed, so a frame allocates nothing.</summary>
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

    /// <summary>The width the list picker was last measured at, for which lists and theme.</summary>
    private sealed class MeasuredWidth
    {
        public int Revision = -1;
        public UiTheme? Theme;
        public float Width;
    }

    private static void ListItem(Ui<UiState> ui, SavedAssemblyLists lists, string name)
    {
        var active = name == lists.Active;
        if (!ui.MenuRadioItem(name, active, id: ListItemPrefix + name))
        {
            return;
        }

        Commands.Later(ui.State, name, static (a, list) => SwitchAssemblyList(a, list));
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
                Commands.Later(ui.State, version, static (a, v) => SetLanguageVersion(a, v));
            }
        }
    }

    private static Ui.Scope Group(Ui<UiState> ui)
    {
        return ui.Row(new Box { Width = Sizing.Fit(), Gap = ui.Theme.Gap, AlignY = Align.Center });
    }

    private static void HistoryItems(Ui<UiState> ui, NavigationHistory history, int direction)
    {
        var entries = history.Entries;
        for (var i = history.CurrentIndex + direction; i >= 0 && i < entries.Count; i += direction)
        {
            using (ui.Id(i))
            {
                var entry = entries[i];
                var text = TextOf(entry);
                var icon = IconOf(ui.State, entry);
                if (ui.MenuItem(text, id: "entry", icon: icon))
                {
                    Commands.Later(ui.State, i, static (a, index) => GoToHistoryEntry(a, index));
                    return;
                }
            }
        }
    }

    private static string TextOf(HistoryEntry entry)
    {
        return entry switch
        {
            HistoryEntry.Node node => node.Place.Label,
            HistoryEntry.Diagram diagram => diagram.Title,
            _ => throw new UnreachableException(),
        };
    }

    private static UiIcon IconOf(UiState screen, HistoryEntry entry)
    {
        return entry switch
        {
            HistoryEntry.Node node => NodeIcons.Icon(node.Icon, node.Accessibility, screen.NodeIconPalette, screen.Icons),
            HistoryEntry.Diagram => new UiIcon(screen.Icons.TabDiagram),
            _ => throw new UnreachableException(),
        };
    }
}
