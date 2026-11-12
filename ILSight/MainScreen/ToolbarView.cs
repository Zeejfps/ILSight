using System.Diagnostics;
using System.Runtime.CompilerServices;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using ILSight.Commanding;
using ILSight.AssemblyListKeeping;
using static ILSight.AssemblyListKeeping.AssemblyLists;
using static ILSight.Preferences;
using static ILSight.ScreenLayout;

namespace ILSight.MainScreen;

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
                ListPicker(ui, appState);
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
                    MainMenuView.OpenFile(ui, appState.Intent);
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
                        Commands.MoveLater(ui.State, appState.Navigation.GoBack(appState.Assemblies, appState.Language, appState.DecompilerOptions, appState.PreviewTabs));
                    }

                    if (back.IsOpen)
                    {
                        HistoryItems(ui, appState.Navigation, appState.Assemblies, appState.Language, appState.DecompilerOptions, appState.PreviewTabs, -1);
                    }
                }
                using (var forward = ui.SplitIconButton("forward", ui.State.Icons.ToolbarForward,
                           ui.Memo("forward-tip", t, static s => s.ToolbarForward(Shortcuts.Forward)), t.ToolbarForwardHistory, appState.History.CanGoForward,
                           appState.History.Revision))
                {
                    if (forward.Clicked)
                    {
                        Commands.MoveLater(ui.State, appState.Navigation.GoForward(appState.Assemblies, appState.Language, appState.DecompilerOptions, appState.PreviewTabs));
                    }

                    if (forward.IsOpen)
                    {
                        HistoryItems(ui, appState.Navigation, appState.Assemblies, appState.Language, appState.DecompilerOptions, appState.PreviewTabs, 1);
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
    private static void ListPicker(Ui<UiState> ui, AppState appState)
    {
        var t = ui.State.Strings;
        var lists = appState.Lists;
        var width = ListPickerWidth(ui, lists);
        using var menu = ui.SelectButton("list", lists.Active, width, t.ListsPick, revision: lists.Count);
        if (!menu.IsOpen)
        {
            return;
        }

        foreach (var name in lists.Names)
        {
            ListItem(ui, appState, name);
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

    /// <summary>The menu item of the list named <paramref name="name"/>; picked, the list is switched to in this
    /// frame (<see cref="AssemblyLists.SwitchAssemblyList"/>, over the whole session), and only opening its files is
    /// queued. The picker drawn above shows the list next frame, which this asks for.</summary>
    private static void ListItem(Ui<UiState> ui, AppState appState, string name)
    {
        var active = name == appState.Lists.Active;
        if (!ui.MenuRadioItem(name, active, id: ListItemPrefix + name))
        {
            return;
        }

        Commands.OpenListLater(ui.State, SwitchAssemblyList(appState, name));
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
                Commands.Later(ui.State, version, static (a, v) => SetLanguageVersion(a, v));
            }
        }
    }

    private static Ui.Scope Group(Ui<UiState> ui)
    {
        return ui.Row(new Box { Width = Sizing.Fit(), Gap = ui.Theme.Gap, AlignY = Align.Center });
    }

    /// <summary>The entries of <paramref name="navigation"/>'s history before the current one, or after it when
    /// <paramref name="direction"/> is positive; the one picked is gone to in this frame, and only its load or lookup is
    /// queued.</summary>
    private static void HistoryItems(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies, DecompilationLanguage language, DecompilerOptions options,
        bool previewTabs, int direction)
    {
        var history = navigation.History;
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
                    Commands.MoveLater(ui.State, navigation.GoToEntry(i, assemblies, language, options, previewTabs));
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
