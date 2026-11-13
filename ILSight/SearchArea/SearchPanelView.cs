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
    public static void Render(Ui<UiState> ui, Navigation navigation, DecompilerSetup setup)
    {
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f });
        using (ui.PaneToolbar(new Box { Gap = ui.Theme.Gap }))
        {
            KindsButton(ui);
            Field(ui, navigation, setup);
            ResultCount(ui, ui.State.Search.Results);
        }
        ui.Separator();
        Unsearched(ui, ui.State.Search.Results);
        Results(ui, navigation, setup);
    }

    /// <summary>A line for each assembly the search couldn't search, saying why, above the hits.</summary>
    private static void Unsearched(Ui<UiState> ui, SearchResults results)
    {
        var t = ui.State.Strings;
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
    }

    private static void KindsButton(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var kinds = ui.State.Search.Kinds;
        var summary = ui.Memo("search-kinds", (kinds, t), static a => Summary(a.t, a.kinds));
        var width = ui.SelectButtonWidth(Summaries.GetValue(t, ListAllSummaries));
        var tooltip = ui.Memo("search-kinds-tip", (kinds, t), static a => Description(a.t, a.kinds));
        using var menu = ui.SelectButton("search-kinds", summary, width, tooltip);
        if (menu.IsOpen)
        {
            KindsMenu(ui);
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
            return $"{t.SearchMode}: {t.SearchKindsAll}";
        }

        var labels = ListPicked(kinds).Select(k => KindLabel(t, k));
        return $"{t.SearchMode}:\n{string.Join("\n", labels)}";
    }

    /// <summary>A check item for all kinds over one per kind, kept open so several can be flipped
    /// in a row; <see cref="SearchKindChoice"/> decides what each leaves picked.</summary>
    private static void KindsMenu(Ui<UiState> ui)
    {
        var search = ui.State.Search;
        var kinds = PickKinds(ui, search.Kinds);
        if (kinds == search.Kinds)
        {
            return;
        }

        search.LookFor(kinds);
    }

    /// <summary>The kinds the menu's items leave picked, <paramref name="kinds"/> picked before.</summary>
    private static SearchKinds PickKinds(Ui<UiState> ui, SearchKinds kinds)
    {
        var t = ui.State.Strings;
        var checkAll = kinds == SearchKinds.All;
        var palette = ui.State.NodeIconPalette;
        var allIcon = new UiIcon(ui.State.Icons.SearchAllKinds, ui.Theme.TextMuted);
        if (ui.MenuItem(t.SearchModeAll, ref checkAll, keepOpen: true, icon: allIcon))
        {
            kinds = SearchKinds.All;
        }

        ui.MenuSeparator();
        foreach (var kind in Kinds)
        {
            var on = SearchKindChoice.IsChecked(kinds, kind);
            var icon = NodeIcons.Icon(KindNode(kind), NodeAccessibility.None, palette, ui.State.Icons);
            var label = KindLabel(t, kind);
            if (ui.MenuItem(label, ref on, keepOpen: true, icon: icon))
            {
                kinds = SearchKindChoice.Toggle(kinds, kind, on);
            }
        }

        return kinds;
    }

    private static void Field(Ui<UiState> ui, Navigation navigation, DecompilerSetup setup)
    {
        var t = ui.State.Strings;
        var search = ui.State.Search;
        var fieldId = ui.GetId("search");
        if (search.TryTakeFocus())
        {
            ui.Focus(fieldId);
        }

        var width = Sizing.Grow(AppLayout.SearchMinWidth, AppLayout.SearchWidth);
        var submitted = ui.TextField("search", ref search.Query, t.SearchField, width, ui.State.Icons.SearchField, clearable: true);
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
                Commands.DecompileLater(ui.State, navigation.ShowNode(row.Node, setup));
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
        if (results.SelectedRow == null)
        {
            results.Move(1);
        }

        return results.SelectedRow;
    }

    private static void ResultCount(Ui<UiState> ui, SearchResults results)
    {
        var invalidRegex = results.InvalidRegex;
        if (invalidRegex is not null)
        {
            InvalidRegexNotice(ui, invalidRegex);
            return;
        }

        var t = ui.State.Strings;
        var theme = ui.Theme;
        var count = ui.Memo("search-count", (results.Rows.Count, results.IsBusy, t), static a => CountText(a.t, a.Count, a.IsBusy));
        ui.Text(count, color: theme.TextMuted, size: theme.FontSizeSmall);
    }

    /// <summary>In place of the count: that the query's <c>/regex/</c> is invalid, so nothing is found,
    /// in the warning colour, with why in its tooltip.</summary>
    private static void InvalidRegexNotice(Ui<UiState> ui, SearchMatcher.InvalidRegex invalidRegex)
    {
        var theme = ui.Theme;
        var id = ui.GetId("search-invalid-regex");
        ui.Text(ui.State.Strings.SearchInvalidRegex, color: theme.Warning, size: theme.FontSizeSmall, id: id);
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

    private static void Results(Ui<UiState> ui, Navigation navigation, DecompilerSetup setup)
    {
        var t = ui.State.Strings;
        var results = ui.State.Search.Results;
        var headers = Headers.GetValue(t, static t => [t.SearchColumnName, t.SearchColumnLocation, t.SearchColumnAssembly]);
        var asked = results.AskedOrder;
        var sort = new TableSort(ColumnOf(asked.Sort), SortIcon(ui.State.Icons, asked));
        ItemSelections.ShowIn(results.Selection, results.Rows);
        var table = ui.Table(ResultsId, ui.State.SearchColumns, results.Selection.Rows);
        var clicked = table.Header(headers, sort);
        if (clicked >= 0)
        {
            var sorting = results.StartSort(SortOf(clicked));
            Commands.Start(ui.State, nameof(Searches.SortAsync), (results, sorting), static (_, s, ct) => Searches.SortAsync(s.results, s.sorting, ct),
                sorting.Work.Token);
        }

        ResultRows(ui, navigation, setup, table);
    }

    private const int NoColumn = -1;

    private static int ColumnOf(SearchSort sort)
    {
        return sort switch
        {
            SearchSort.Relevance => NoColumn,
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

    private static void ResultRows(Ui<UiState> ui, Navigation navigation, DecompilerSetup setup,
        TableFrame table)
    {
        var results = ui.State.Search.Results;
        using var body = table.Body(results.Rows.Count);
        ItemSelections.TakeFrom(results.Selection, results.Rows);
        for (var i = body.First; i < body.End; i++)
        {
            ResultRow(ui, navigation, setup, body, i);
        }

        ItemSelections.TakeFrom(results.Selection, results.Rows);
        var selected = results.SelectedRow;
        if (body.Activated && selected != null)
        {
            Commands.DecompileLater(ui.State, navigation.ShowNode(selected.Node, setup, body.InNewTab));
        }
    }

    private static void ResultRow(Ui<UiState> ui, Navigation navigation, DecompilerSetup setup,
        TableBody body, int index)
    {
        var theme = ui.Theme;
        var result = ui.State.Search.Results.Rows[index];
        var node = result.Node;
        var row = body.Row(index, RuntimeHelpers.GetHashCode(node));
        using (row)
        {
            if (row.Interaction.Clicked)
            {
                Commands.DecompileLater(ui.State, navigation.ShowNode(node, setup));
            }

            NameCell(ui, row, result);
            row.TextCell(1, result.Location, theme.TextMuted, overflow: TextOverflow.EllipsisMiddle);
            row.TextCell(2, NodeText.AssemblyLabel(node.Assembly), theme.TextMuted);
        }
        ui.Tooltip(row.Id, NodeText.Tooltip(node));
    }

    private static void NameCell(Ui<UiState> ui, TableRow row, SearchResult result)
    {
        var theme = ui.Theme;
        var node = result.Node;
        var color = row.Selected ? theme.SelectionText : theme.Text;
        using (row.Cell(0))
        {
            NodeIcons.Draw(ui, node);
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
