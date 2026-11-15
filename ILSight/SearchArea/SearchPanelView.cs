using System.Diagnostics;
using System.Runtime.CompilerServices;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ZGF.KeyboardModule;
using ILSight.Core;
using ILSight.CommandArea;

namespace ILSight.SearchArea;

internal static class SearchPanelView
{
    /// <summary>The results list's focus and scroll id.</summary>
    public const int ResultsId = 0x53524C54;

    private static readonly SearchKinds[] Kinds =
    [
        SearchKinds.Types, SearchKinds.Methods, SearchKinds.Fields, SearchKinds.Properties, SearchKinds.Events, SearchKinds.Literals,
        SearchKinds.Tokens, SearchKinds.Resources, SearchKinds.Assemblies, SearchKinds.Namespaces,
    ];

    private static readonly ConditionalWeakTable<Strings, string[]> Summaries = new();
    private static readonly ConditionalWeakTable<Strings, string[]> Headers = new();

    /// <summary>The search panel. A result picked shows in <paramref name="navigation"/>'s tabs in this frame, in
    /// <paramref name="setup"/>, in the preview tab while
    /// <see cref="Navigation.PreviewTabs"/>; only its decompile is queued.</summary>
    public static void Render(Ui ui, Appearance appearance, Search search, SearchPanel panel, IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup)
    {
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        using (ui.PaneToolbar(new Box { Gap = ui.Theme.Gap }))
        {
            KindsButton(ui, appearance, search);
            Field(ui, appearance, search, commands, navigation, setup);
            ResultCount(ui, appearance, search.Results);
        }
        ui.Separator();
        PassedOver(ui, appearance, search.Results);
        Results(ui, appearance, search, panel.Columns, commands, navigation, setup);
    }

    // What the search couldn't read that could have held a hit, a line each: an assembly not searched, one searched in
    // part, a match found and not shown.
    private static void PassedOver(Ui ui, Appearance appearance, SearchResults results)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        for (var i = 0; i < results.Unsearched.Count; i++)
        {
            var unsearched = results.Unsearched[i];
            using (ui.Id(i))
            {
                var line = ui.Memo("unsearched", (unsearched, t), static a => a.t.SearchUnsearched(a.unsearched.Assembly.FilePath.FileName, a.unsearched.Why));
                ui.Text(line, color: theme.Warning, size: theme.FontSizeSmall);
            }
        }

        for (var i = 0; i < results.PartlySearched.Count; i++)
        {
            var partly = results.PartlySearched[i];
            using (ui.Id(i))
            {
                var line = ui.Memo("partly-searched", (partly, t),
                    static a => a.t.SearchPartlySearched(a.partly.Assembly.FilePath.FileName, a.partly.Count, a.partly.Why));
                ui.Text(line, color: theme.Warning, size: theme.FontSizeSmall);
            }
        }

        for (var i = 0; i < results.NotShown.Count; i++)
        {
            var notShown = results.NotShown[i];
            using (ui.Id(i))
            {
                var line = ui.Memo("not-shown", (notShown, t),
                    static a => a.t.SearchFoundNotShown(a.notShown.Name ?? $"0x{a.notShown.Token:X8}", a.notShown.Assembly.FilePath.FileName, a.notShown.Why));
                ui.Text(line, color: theme.Warning, size: theme.FontSizeSmall);
            }
        }
    }

    private static void KindsButton(Ui ui, Appearance appearance, Search search)
    {
        var t = appearance.Strings;
        var kinds = search.Kinds;
        var summary = ui.Memo("search-kinds", (kinds, t), static a => Summary(a.t, a.kinds));
        var width = ui.SelectButtonWidth(Summaries.GetValue(t, ListAllSummaries));
        var tooltip = ui.Memo("search-kinds-tip", (kinds, t), static a => Description(a.t, a.kinds));
        using var menu = ui.SelectButton("search-kinds", summary, width, tooltip);
        if (menu.IsOpen)
        {
            KindsMenu(ui, search, appearance);
        }
    }

    private static string Summary(Strings t, SearchKinds kinds)
    {
        switch (kinds)
        {
            case SearchKinds.All:
                return t.SearchKindsAll;
            case SearchKinds.TypesAndMembers:
                return t.SearchModeTypesAndMembers;
            case SearchKinds.Members:
                return t.SearchModeMembers;
        }
        var picked = ListPicked(kinds);
        if (picked.Length == 1)
        {
            return KindLabel(t, picked[0]);
        }

        return t.SearchKindsCount(picked.Length);
    }

    private static SearchKinds[] ListPicked(SearchKinds kinds)
    {
        return Kinds
            .Where(k => (kinds & k) != 0)
            .ToArray();
    }

    private static string[] ListAllSummaries(Strings t)
    {
        var summaries = new List<string>
        {
            t.SearchKindsAll, t.SearchModeTypesAndMembers, t.SearchModeMembers, t.SearchKindsCount(Kinds.Length),
        };
        foreach (var kind in Kinds)
        {
            summaries.Add(KindLabel(t, kind));
        }

        return summaries.ToArray();
    }

    private static string Description(Strings t, SearchKinds kinds)
    {
        if (kinds == SearchKinds.All)
        {
            return t.SearchKindsTip(t.SearchKindsAll);
        }

        // A kind a line: the lines are layout, the words around them each language's template.
        var labels = ListPicked(kinds).Select(k => KindLabel(t, k));
        return t.SearchKindsTipList(string.Join("\n", labels));
    }

    private static void KindsMenu(Ui ui, Search search, Appearance appearance)
    {
        var kinds = PickKinds(ui, appearance, search.Kinds);
        if (kinds == search.Kinds)
        {
            return;
        }

        search.LookFor(kinds);
    }

    private static SearchKinds PickKinds(Ui ui, Appearance appearance, SearchKinds kinds)
    {
        var t = appearance.Strings;
        var checkAll = kinds == SearchKinds.All;
        var palette = appearance.NodeIconPalette;
        var allIcon = new UiIcon(appearance.Icons.SearchAllKinds, ui.Theme.TextMuted);
        // The items keep the menu open, so several can be flipped in a row.
        if (ui.MenuItem(t.SearchModeAll, ref checkAll, keepOpen: true, icon: allIcon))
        {
            kinds = SearchKinds.All;
        }

        ui.MenuSeparator();
        foreach (var kind in Kinds)
        {
            var on = SearchKindChoice.IsChecked(kinds, kind);
            var icon = NodeIcons.Icon(KindNode(kind), NodeAccessibility.None, palette, appearance.Icons);
            var label = KindLabel(t, kind);
            if (ui.MenuItem(label, ref on, keepOpen: true, icon: icon))
            {
                kinds = SearchKindChoice.Toggle(kinds, kind, on);
            }
        }

        return kinds;
    }

    private static void Field(Ui ui, Appearance appearance, Search search, IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup)
    {
        var t = appearance.Strings;
        var fieldId = ui.GetId("search");
        if (search.TryTakeFocus())
        {
            ui.Focus(fieldId);
        }

        var width = Sizing.Grow(AppLayout.SearchMinWidth, AppLayout.SearchWidth);
        var submitted = ui.TextField("search", ref search.Query, t.SearchField, width, appearance.Icons.SearchField, clearable: true);
        ui.Tooltip(fieldId, ui.Memo("search-syntax", t, static s => s.SearchSyntaxTooltip(Shortcuts.Search.Label)));
        if (!ui.HasFocus(fieldId))
        {
            return;
        }

        if (submitted)
        {
            var row = PickSelected(search.Results);
            if (row != null)
            {
                Commands.DecompileLater(commands, navigation.ShowNode(row.Node, setup));
            }

            return;
        }
        if (ui.Input.IsKeyPressed(KeyboardKey.DownArrow) && search.Results.Rows.Count > 0)
        {
            ui.Focus(ResultsId, takesText: false);
        }
    }

    private static SearchResult? PickSelected(SearchResults results)
    {
        if (results.Selection.Item == null)
        {
            results.Move(1);
        }

        return results.Selection.Item;
    }

    private static void ResultCount(Ui ui, Appearance appearance, SearchResults results)
    {
        var invalidRegex = results.InvalidRegex;
        if (invalidRegex is not null)
        {
            InvalidRegexNotice(ui, appearance, invalidRegex);
            return;
        }

        var t = appearance.Strings;
        var theme = ui.Theme;
        var failure = results.RunFailure;
        if (failure is not null)
        {
            var failed = ui.Memo("search-failed", (failure, t), static a => a.t.SearchFailed(a.failure));
            ui.Text(failed, color: theme.Warning, size: theme.FontSizeSmall);
            return;
        }

        var count = ui.Memo("search-count", (results.Rows.Count, results.IsBusy, t), static a => CountText(a.t, a.Count, a.IsBusy));
        ui.Text(count, color: theme.TextMuted, size: theme.FontSizeSmall);
    }

    private static void InvalidRegexNotice(Ui ui, Appearance appearance, SearchMatcher.InvalidRegex invalidRegex)
    {
        var theme = ui.Theme;
        var id = ui.GetId("search-invalid-regex");
        ui.Text(appearance.Strings.SearchInvalidRegex, color: theme.Warning, size: theme.FontSizeSmall, id: id);
        ui.Tooltip(id, invalidRegex.Message);
    }

    private static string CountText(Strings t, int count, bool isBusy)
    {
        if (isBusy)
        {
            return t.SearchResultsSoFar(count);
        }

        return t.SearchResults(count);
    }

    private static void Results(Ui ui, Appearance appearance, Search search, TableColumns searchColumns, IAppCommandWriter commands, Navigation navigation, DecompilerSetup setup)
    {
        var t = appearance.Strings;
        var results = search.Results;
        var headers = Headers.GetValue(t, static t => [t.SearchColumnName, t.SearchColumnLocation, t.SearchColumnAssembly]);
        var asked = results.AskedOrder;
        var sort = new TableSort(FindColumn(asked.Sort) ?? -1, SortIcon(appearance.Icons, asked));
        results.Selection.ShowIn(results.Rows);
        var table = ui.Table(ResultsId, searchColumns, results.Selection.Rows);
        var clicked = table.Header(headers, sort);
        if (clicked >= 0)
        {
            var sorting = results.StartSort(SortOf(clicked));
            Commands.StartSettling(commands, nameof(Searches.SortAsync), (results, sorting), static (_, s, ct) => Searches.SortAsync(s.results, s.sorting, ct),
                static (_, s, _) => s.results.FailSort(s.sorting), sorting.Work.Token);
        }

        ResultRows(ui, search, commands, appearance, navigation, setup, table);
    }

    private static int? FindColumn(SearchSort sort)
    {
        return sort switch
        {
            SearchSort.Relevance => null,
            SearchSort.Name => 0,
            SearchSort.Location => 1,
            SearchSort.Assembly => 2,
            _ => throw new UnreachableException(),
        };
    }

    private static UiIcon SortIcon(AppIcons icons, SearchOrder order)
    {
        var glyph = order.Descending ? icons.SortDescending : icons.SortAscending;
        return new UiIcon(glyph);
    }

    private static SearchSort SortOf(int column)
    {
        return column switch
        {
            0 => SearchSort.Name,
            1 => SearchSort.Location,
            2 => SearchSort.Assembly,
            _ => throw new UnreachableException(),
        };
    }

    private static void ResultRows(Ui ui, Search search, IAppCommandWriter commands, Appearance appearance, Navigation navigation, DecompilerSetup setup,
        TableFrame table)
    {
        var results = search.Results;
        using var body = table.Body(results.Rows.Count);
        results.Selection.TakeFrom(results.Rows);
        for (var i = body.First; i < body.End; i++)
        {
            ResultRow(ui, search, commands, appearance, navigation, setup, body, i);
        }

        results.Selection.TakeFrom(results.Rows);
        var selected = results.Selection.Item;
        if (body.Activated && selected != null)
        {
            Commands.DecompileLater(commands, navigation.ShowNode(selected.Node, setup, body.InNewTab));
        }
    }

    private static void ResultRow(Ui ui, Search search, IAppCommandWriter commands, Appearance appearance, Navigation navigation, DecompilerSetup setup,
        TableBody body, int index)
    {
        var theme = ui.Theme;
        var result = search.Results.Rows[index];
        var node = result.Node;
        var row = body.Row(index, RuntimeHelpers.GetHashCode(node));
        using (row)
        {
            if (row.Interaction.Clicked)
            {
                Commands.DecompileLater(commands, navigation.ShowNode(node, setup));
            }

            NameCell(ui, appearance, row, result);
            row.TextCell(1, result.Location, theme.TextMuted, overflow: TextOverflow.EllipsisMiddle);
            row.TextCell(2, NodeText.AssemblyLabel(node.Assembly), theme.TextMuted);
        }
        ui.Tooltip(row.Id, NodeText.Tooltip(appearance.Strings, node));
    }

    private static void NameCell(Ui ui, Appearance appearance, TableRow row, SearchResult result)
    {
        var theme = ui.Theme;
        var node = result.Node;
        var color = row.Selected ? theme.SelectionText : theme.Text;
        using (row.Cell(0))
        {
            NodeIcons.Draw(ui, appearance, node);
            ui.Text(result.Text, new Box { Width = Sizing.Grow() }, color: color, overflow: TextOverflow.Ellipsis);
        }
    }

    private static string KindLabel(Strings t, SearchKinds kind)
    {
        return kind switch
        {
            SearchKinds.Types => t.SearchModeTypes,
            SearchKinds.Methods => t.SearchModeMethods,
            SearchKinds.Fields => t.SearchModeFields,
            SearchKinds.Properties => t.SearchModeProperties,
            SearchKinds.Events => t.SearchModeEvents,
            SearchKinds.Literals => t.SearchModeLiterals,
            SearchKinds.Tokens => t.SearchModeToken,
            SearchKinds.Resources => t.SearchModeResources,
            SearchKinds.Assemblies => t.SearchModeAssemblies,
            SearchKinds.Namespaces => t.SearchModeNamespaces,
            _ => throw new UnreachableException(),
        };
    }

    private static NodeIcon KindNode(SearchKinds kind)
    {
        return kind switch
        {
            SearchKinds.Types => NodeIcon.Class,
            SearchKinds.Methods => NodeIcon.Method,
            SearchKinds.Fields => NodeIcon.Field,
            SearchKinds.Properties => NodeIcon.Property,
            SearchKinds.Events => NodeIcon.Event,
            SearchKinds.Literals => NodeIcon.Constant,
            SearchKinds.Tokens => NodeIcon.MetadataTable,
            SearchKinds.Resources => NodeIcon.Resource,
            SearchKinds.Assemblies => NodeIcon.Assembly,
            SearchKinds.Namespaces => NodeIcon.Namespace,
            _ => throw new UnreachableException(),
        };
    }
}
