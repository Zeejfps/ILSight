using System.Runtime.CompilerServices;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;
using ZGF.KeyboardModule;
using static ILSight.Navigation;

namespace ILSight;

/// <summary>The bar over a document: the path to what it shows, a crumb per node with an arrow after it
/// that lists the node's children, or the tools of a diagram or comparison it shows, and a search button; or, while it searches, the search box with the
/// best matches under it. A crumb or child picked shows as one picked in the tree does; a match goes
/// where a search result goes. Ctrl or the middle button opens either in a new tab.</summary>
internal static class OmnibarView
{
    private const string SearchFieldId = "omnibar-search";
    private const string FilterFieldId = "omnibar-filter";
    private const int MenuSalt = 0x4F4D4E49;
    private const int ListSalt = 0x4F4D4C53;
    private const int SuggestionsSalt = 0x4F4D5347;
    private const float MenuWidth = 320f;
    private const int MenuRows = 12;
    private const float CrumbMinWidth = 24f;

    public static bool IsSearchingIn(Omnibar omnibar, string panel)
    {
        return omnibar.SearchingPanel == panel;
    }

    /// <summary>Has the bar of <paramref name="panel"/> search, from an empty query, its box taking focus.</summary>
    public static void StartSearch(Omnibar omnibar, string panel)
    {
        omnibar.Searching = new Omnibar.OmnibarSearch { Panel = panel };
        Searches.Clear(omnibar.Search);
        omnibar.Menu = null;
    }

    /// <summary>Has every bar show its path again.</summary>
    public static void StopSearch(Omnibar omnibar)
    {
        omnibar.Searching = null;
        Searches.Clear(omnibar.Search);
    }

    /// <summary>Whether the list of <paramref name="node"/>'s children is open in the bar of <paramref name="panel"/>.</summary>
    public static bool IsMenuOpenAt(Omnibar omnibar, string panel, BrowseNode node)
    {
        var menu = omnibar.Menu;
        if (menu == null)
            return false;
        if (menu.Panel != panel)
            return false;
        return ReferenceEquals(menu.Node, node);
    }

    /// <summary>Lists the children of <paramref name="node"/> in the bar of <paramref name="panel"/>, with
    /// <paramref name="next"/>, the crumb after it, to be selected.</summary>
    public static void OpenMenu(Omnibar omnibar, string panel, BrowseNode node, BrowseNode? next)
    {
        omnibar.Menu = new CrumbMenu { Panel = panel, Node = node, ToSelect = next };
    }

    /// <summary>True once after <see cref="StartSearch"/>: the search box should take focus.</summary>
    private static bool TakeFocus(Omnibar omnibar)
    {
        var searching = omnibar.Searching;
        if (searching == null || !searching.IsFocusPending)
            return false;
        searching.IsFocusPending = false;
        return true;
    }

    /// <summary>The suggestion to go to: the one selected, else the best match; null when none is suggested.</summary>
    private static SearchResult? FindSuggestionToShow(Omnibar omnibar)
    {
        var selected = omnibar.SelectedSuggestion;
        if (selected != null)
            return selected;
        if (omnibar.SuggestionCount == 0)
            return null;
        return omnibar.Search.Rows[0];
    }

    /// <summary>Selects the suggestion <paramref name="by"/> rows on from the selected one, kept within those suggested.</summary>
    private static void MoveSuggestion(Omnibar omnibar, int by)
    {
        if (omnibar.SuggestionCount == 0)
            return;
        Searches.Move(omnibar.Search, by);
        var index = ItemSelections.FindIndexIn(omnibar.Search.Selection, omnibar.Search.Rows);
        if (index < omnibar.SuggestionCount)
            return;
        Searches.Select(omnibar.Search, omnibar.Search.Rows[omnibar.SuggestionCount - 1]);
    }

    /// <summary>True once after <see cref="OpenMenu"/>: the filter should take focus.</summary>
    private static bool TakeFocus(CrumbMenu menu)
    {
        var focus = menu.IsFocusPending;
        menu.IsFocusPending = false;
        return focus;
    }

    /// <summary>The <paramref name="children"/> whose name, as <paramref name="tree"/> labels their rows, holds
    /// the filter; filtered again only when the children or the filter changed. Runs each frame the menu is
    /// open, so what filters is a function of its own: a lambda here would allocate its closure at the
    /// top of this function, on the way to returning what is already known.</summary>
    private static IReadOnlyList<BrowseNode> ListShown(CrumbMenu menu, IReadOnlyList<BrowseNode> children, BrowseTree tree)
    {
        if (IsShowing(menu, children))
            return menu.Shown;
        FilterChildren(menu, children, tree);
        return menu.Shown;
    }

    private static bool IsShowing(CrumbMenu menu, IReadOnlyList<BrowseNode> children)
    {
        if (!ReferenceEquals(children, menu.Children))
            return false;
        return menu.Filter == menu.ShownFilter;
    }

    private static void FilterChildren(CrumbMenu menu, IReadOnlyList<BrowseNode> children, BrowseTree tree)
    {
        menu.Children = children;
        menu.ShownFilter = menu.Filter;
        menu.Shown.Clear();
        menu.Shown.AddRange(children.Where(c => HoldsFilter(menu, tree.RowLabel(c))));
        SelectWaitingChild(menu);
        ItemSelections.DropIfGone(menu.Selection, menu.Shown);
        SelectFirstMatch(menu);
    }

    private static bool HoldsFilter(CrumbMenu menu, string name)
    {
        return name.Contains(menu.Filter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Selects the crumb after this one once the children built hold it.</summary>
    private static void SelectWaitingChild(CrumbMenu menu)
    {
        if (menu.ToSelect == null)
            return;
        if (!menu.Shown.Contains(menu.ToSelect))
            return;
        ItemSelections.Select(menu.Selection, menu.ToSelect, menu.Shown);
        menu.ToSelect = null;
    }

    /// <summary>Selects the first child listed while a filter leaves none selected, for Enter to go to.</summary>
    private static void SelectFirstMatch(CrumbMenu menu)
    {
        if (menu.Filter.Length == 0)
            return;
        if (menu.Selection.Item != null)
            return;
        if (menu.Shown.Count == 0)
            return;
        ItemSelections.Select(menu.Selection, menu.Shown[0], menu.Shown);
    }

    /// <summary>Follows <paramref name="node"/> as <paramref name="tree"/> shows it; none when it is null.
    /// Runs each frame, so listing the crumbs is a function of its own, for the reason <see cref="ListShown"/> gives.</summary>
    private static void FollowPath(Breadcrumb breadcrumb, BrowseNode? node, BrowseTree tree)
    {
        if (IsCurrent(breadcrumb, node, tree))
            return;
        breadcrumb.Node = node;
        breadcrumb.TreeRevision = tree.Revision;
        breadcrumb.Crumbs = ListCrumbs(node, tree);
    }

    private static bool IsCurrent(Breadcrumb breadcrumb, BrowseNode? node, BrowseTree tree)
    {
        if (!ReferenceEquals(node, breadcrumb.Node))
            return false;
        return tree.Revision == breadcrumb.TreeRevision;
    }

    private static Crumb[] ListCrumbs(BrowseNode? node, BrowseTree tree)
    {
        if (node == null)
            return [];
        return tree.ListPathTo(node)
            .Select(n => new Crumb(n, GetCrumbLabel(n, tree)))
            .ToArray();
    }

    /// <summary>An assembly's name as its tab titles it, with the version that tells apart those of one
    /// name; any other node's as its row labels it.</summary>
    private static string GetCrumbLabel(BrowseNode node, BrowseTree tree)
    {
        if (node.Subject is NodeSubject.Module)
            return tree.Label(node);
        return tree.RowLabel(node);
    }

    public static void Render(Ui<UiState> ui, DocumentTab tab)
    {
        if (!ui.State.Display.ShowOmnibar)
            return;
        var panel = DocumentPanels.GetPanel(ui.State.Documents, tab);
        if (ContinueSearch(ui, panel))
            SearchBar(ui);
        else
            PathBar(ui, tab, panel);
        ui.Separator();
    }

    /// <summary>Whether the bar of <paramref name="panel"/> searches: its box takes focus when the search
    /// just started, and the search stops once the box lost focus.</summary>
    private static bool ContinueSearch(Ui<UiState> ui, string panel)
    {
        var omnibar = ui.State.Omnibar;
        if (!IsSearchingIn(omnibar, panel))
            return false;
        var fieldId = ui.GetId(SearchFieldId);
        if (TakeFocus(omnibar))
        {
            ui.Focus(fieldId);
            return true;
        }
        if (ui.HasFocus(fieldId))
            return true;
        StopSearch(omnibar);
        return false;
    }

    private static void PathBar(Ui<UiState> ui, DocumentTab tab, string panel)
    {
        var view = DocumentPanels.GetView(ui.State.Documents, tab);
        FollowPath(view.Breadcrumb, tab.Node, ui.State.Tree);
        var crumbs = view.Breadcrumb.Crumbs;
        var menuOwner = 0;
        using (ui.PaneToolbar(new Box { Gap = 0f }))
        {
            for (var i = 0; i < crumbs.Count; i++)
            {
                using (ui.Id(i))
                {
                    CrumbButton(ui, crumbs[i], i);
                    var owner = ChildrenButton(ui, panel, crumbs, i);
                    if (owner != 0)
                        menuOwner = owner;
                }
            }
            ContentTools(ui, tab);
            SearchArea(ui, panel);
            SearchButton(ui, panel);
        }
        ChildrenMenu(ui, panel, menuOwner);
    }

    /// <summary>The tools of what the tab shows that no node stands for: a diagram's or a comparison's.</summary>
    private static void ContentTools(Ui<UiState> ui, DocumentTab tab)
    {
        switch (tab.Content)
        {
            case TabContent.Diagram:
                DiagramView.Tools(ui, tab);
                break;
            case TabContent.Comparison comparison:
                ComparisonView.Tools(ui, tab, comparison.Assemblies);
                break;
        }
    }

    /// <summary>The node's icon and name, which gives up room, earlier crumbs first, while the path is
    /// too long for the bar.</summary>
    private static void CrumbButton(Ui<UiState> ui, Crumb crumb, int depth)
    {
        var theme = ui.Theme;
        var node = crumb.Node;
        var i = ui.Interact("crumb");
        var width = Sizing.Shrink(CrumbMinWidth) with { Priority = depth };
        using (ui.Row(i.Id, new Box
        {
            Width = width,
            Height = theme.IconButtonSize,
            Padding = new Spacing(theme.GapTight, 0f),
            Gap = theme.GapTight,
            AlignY = Align.Center,
            Background = theme.ControlFill(i.Held, i.Hovered),
            Radius = theme.CornerRadius,
        }))
        {
            NodeIcons.Draw(ui, node);
            ui.Text(crumb.Label, new Box { Width = Sizing.Shrink() }, color: CrumbColor(theme, node), overflow: TextOverflow.Ellipsis);
        }
        ui.Tooltip(i.Id, NodeText.Tooltip(node));
        if (i.Clicked)
            ui.State.Later((node, inNewTab: IsCommandHeld(ui)), static (a, s) => PickNode(a, s.node, s.inNewTab));
        if (i.ClickedWith(PointerButton.Middle))
            ui.State.Later(node, static (a, n) => ShowNode(a, n, inNewTab: true));
    }

    /// <summary>Public API in the text color, the rest muted, as the tree shows them.</summary>
    private static uint CrumbColor(UiTheme theme, BrowseNode node)
    {
        if (NodeDefinitions.GetVisibility(node.Subject).IsPublicApi)
            return theme.Text;
        return theme.TextMuted;
    }

    /// <summary>Shows <paramref name="node"/> as one picked in the tree is, or in a new tab when <paramref name="inNewTab"/>.</summary>
    private static void PickNode(AppState appState, BrowseNode node, bool inNewTab)
    {
        if (inNewTab)
        {
            ShowNode(appState, node, inNewTab: true);
            return;
        }
        PreviewNode(appState, node);
    }

    private static bool IsCommandHeld(Ui<UiState> ui) => (ui.Input.Modifiers & Shortcut.CommandModifier) != 0;

    /// <summary>The arrow after a crumb that has children, which opens and closes the list of them, pointing
    /// down while it is open. Returns the arrow's id while its list is open, else 0.</summary>
    private static int ChildrenButton(Ui<UiState> ui, string panel, IReadOnlyList<Crumb> crumbs, int index)
    {
        var node = crumbs[index].Node;
        if (!ui.State.Tree.HasChildren(node))
            return 0;
        var theme = ui.Theme;
        var omnibar = ui.State.Omnibar;
        var i = ui.Interact("children");
        if (i.Clicked)
            ToggleChildren(omnibar, panel, crumbs, index);
        var open = IsMenuOpenAt(omnibar, panel, node);
        using (ui.Box(i.Id, new Box
        {
            Width = theme.IconSize,
            Height = theme.IconButtonSize,
            Background = theme.ControlFill(i.Held || open, i.Hovered),
            Radius = theme.CornerRadius,
        }))
        {
            ui.PaintGlyph(ArrowOf(open), theme.TextMuted);
        }
        if (!open)
            ui.Tooltip(i.Id, ui.Memo("contents-tip", (crumbs[index].Label, ui.State.Strings), static a => a.Strings.OmnibarContents(a.Label)));
        if (!open)
            return 0;
        return i.Id;
    }

    private static void ToggleChildren(Omnibar omnibar, string panel, IReadOnlyList<Crumb> crumbs, int index)
    {
        var node = crumbs[index].Node;
        if (IsMenuOpenAt(omnibar, panel, node))
        {
            omnibar.Menu = null;
            return;
        }
        OpenMenu(omnibar, panel, node, FindNextNode(crumbs, index));
    }

    private static BrowseNode? FindNextNode(IReadOnlyList<Crumb> crumbs, int index)
    {
        var next = index + 1;
        if (next >= crumbs.Count)
            return null;
        return crumbs[next].Node;
    }

    private static UiGlyph ArrowOf(bool open) => open ? UiGlyph.DisclosureOpen : UiGlyph.DisclosureClosed;

    /// <summary>The room left after the path, which starts a search when clicked.</summary>
    private static void SearchArea(Ui<UiState> ui, string panel)
    {
        var i = ui.Interact("search-area");
        if (i.Hovered)
            ui.SetCursor(MouseCursor.Text);
        ui.Leaf(i.Id, new Box { Width = Sizing.Grow(), Height = Sizing.Grow() });
        if (i.Clicked)
            StartSearch(ui.State.Omnibar, panel);
    }

    private static void SearchButton(Ui<UiState> ui, string panel)
    {
        var t = ui.State.Strings;
        var tooltip = ui.Memo("omnibar-search-tip", t, static s => s.SearchFieldTooltip(Shortcuts.OmnibarSearch));
        if (ui.IconButton("search", ui.State.Icons.SearchField, tooltip))
            StartSearch(ui.State.Omnibar, panel);
    }

    /// <summary>The list of the open crumb's children under its arrow <paramref name="owner"/>, with a
    /// filter over them; 0 when no list is open in this bar, which forgets one left open on a crumb gone
    /// from it.</summary>
    private static void ChildrenMenu(Ui<UiState> ui, string panel, int owner)
    {
        var omnibar = ui.State.Omnibar;
        var menu = omnibar.Menu;
        if (menu == null)
            return;
        if (owner == 0)
        {
            CloseIfIn(omnibar, menu, panel);
            return;
        }
        var theme = ui.Theme;
        var isOpen = true;
        using var flyout = ui.Flyout(HashCode.Combine(owner, MenuSalt), ref isOpen, owner, new Box { Width = MenuWidth, Gap = theme.GapTight });
        if (!isOpen)
            omnibar.Menu = null;
        if (!flyout.IsOpen)
            return;
        var children = ui.State.Tree.GetChildren(menu.Node);
        if (children.PlaceholderRow is TreeRow placeholder)
        {
            ui.Text(placeholder.Label, color: theme.TextMuted);
            return;
        }
        var shown = ListShown(menu, children.Nodes, ui.State.Tree);
        var listId = HashCode.Combine(owner, ListSalt);
        var submitted = Filter(ui, menu, shown);
        var picked = ChildRows(ui, menu, shown, listId);
        if (submitted)
            picked = new PickedChild(menu.Selection.Item, IsCommandHeld(ui));
        var node = picked.Node;
        if (node == null)
            return;
        omnibar.Menu = null;
        ui.State.Later((node, inNewTab: picked.InNewTab), static (a, s) => PickNode(a, s.node, s.inNewTab));
    }

    private static void CloseIfIn(Omnibar omnibar, CrumbMenu menu, string panel)
    {
        if (menu.Panel == panel)
            omnibar.Menu = null;
    }

    /// <summary>The filter, which takes focus as the list opens and walks the list with Up and Down.
    /// Returns whether Enter was pressed in it.</summary>
    private static bool Filter(Ui<UiState> ui, CrumbMenu menu, IReadOnlyList<BrowseNode> shown)
    {
        var fieldId = ui.GetId(FilterFieldId);
        if (TakeFocus(menu))
            ui.Focus(fieldId);
        var submitted = ui.TextField(FilterFieldId, ref menu.Filter, ui.State.Strings.OmnibarFilter, Sizing.Grow());
        if (!ui.HasFocus(fieldId))
            return false;
        foreach (var stroke in ui.Input.Keys)
            MoveSelection(menu.Selection, shown, stroke.Key);
        return submitted;
    }

    private static void MoveSelection(ItemSelection<BrowseNode> selection, IReadOnlyList<BrowseNode> shown, KeyboardKey key)
    {
        if (key == KeyboardKey.DownArrow)
            ItemSelections.Move(selection, 1, shown);
        if (key == KeyboardKey.UpArrow)
            ItemSelections.Move(selection, -1, shown);
    }

    /// <summary>A child picked from the list, and whether it is to open in a new tab.</summary>
    private readonly record struct PickedChild(BrowseNode? Node, bool InNewTab);

    /// <summary>The children listed, as tall as up to <see cref="MenuRows"/> of them; returns the one
    /// clicked, if any.</summary>
    private static PickedChild ChildRows(Ui<UiState> ui, CrumbMenu menu, IReadOnlyList<BrowseNode> shown, int listId)
    {
        var rowHeight = ui.Theme.RowHeight;
        var height = Math.Min(shown.Count, MenuRows) * rowHeight;
        var picked = new PickedChild(null, false);
        ItemSelections.ShowIn(menu.Selection, shown);
        using (var list = ui.SelectableList(listId, shown.Count, menu.Selection.Rows, rowHeight, new Box { Width = Sizing.Grow(), Height = height },
                   keys: false, selectOnClick: true))
        {
            for (var i = list.First; i < list.End; i++)
            {
                var row = ChildRow(ui, list, shown[i], i);
                if (row.Node != null)
                    picked = row;
            }
        }
        ItemSelections.TakeFrom(menu.Selection, shown);
        return picked;
    }

    private static PickedChild ChildRow(Ui<UiState> ui, ListScope list, BrowseNode child, int index)
    {
        var theme = ui.Theme;
        using var id = ui.Id(RuntimeHelpers.GetHashCode(child));
        using var row = list.Row(index, ui.GetId("child"), new Box { Gap = theme.GapTight });
        var color = row.Selected ? theme.SelectionText : CrumbColor(theme, child);
        NodeIcons.Draw(ui, child);
        ui.Text(ui.State.Tree.RowLabel(child), new Box { Width = Sizing.Grow() }, color: color, overflow: TextOverflow.Ellipsis);
        if (row.Interaction.Clicked)
            return new PickedChild(child, IsCommandHeld(ui));
        if (row.Interaction.ClickedWith(PointerButton.Middle))
            return new PickedChild(child, true);
        return new PickedChild(null, false);
    }

    /// <summary>The search box, whose Enter goes to the selected match, else the best, and whose Up and
    /// Down walk the matches suggested under it.</summary>
    private static void SearchBar(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var omnibar = ui.State.Omnibar;
        var fieldId = ui.GetId(SearchFieldId);
        bool submitted;
        using (ui.PaneToolbar())
        {
            submitted = ui.TextField(SearchFieldId, ref omnibar.Search.Query, t.OmnibarSearch, Sizing.Grow(), ui.State.Icons.SearchField);
            SyntaxTooltip(ui, omnibar, fieldId);
        }
        WalkSuggestions(ui, omnibar, fieldId);
        var clicked = Suggestions(ui, omnibar, fieldId);
        if (submitted)
            clicked = new PickedMatch(FindSuggestionToShow(omnibar), IsCommandHeld(ui));
        var match = clicked.Match;
        if (match == null)
            return;
        StopSearch(omnibar);
        ui.State.Later((node: match.Node, inNewTab: clicked.InNewTab), static (a, s) => ShowNode(a, s.node, s.inNewTab));
    }

    /// <summary>The query syntax, over the box while it is empty, so it never covers the suggestions.</summary>
    private static void SyntaxTooltip(Ui<UiState> ui, Omnibar omnibar, int fieldId)
    {
        if (omnibar.Search.Query.Length > 0)
            return;
        var t = ui.State.Strings;
        ui.Tooltip(fieldId, ui.Memo("omnibar-syntax", t, static s => s.SearchSyntaxTooltip(Shortcuts.OmnibarSearch)));
    }

    private static void WalkSuggestions(Ui<UiState> ui, Omnibar omnibar, int fieldId)
    {
        if (!ui.HasFocus(fieldId))
            return;
        foreach (var stroke in ui.Input.Keys)
            MoveSuggestion(omnibar, stroke.Key);
    }

    private static void MoveSuggestion(Omnibar omnibar, KeyboardKey key)
    {
        if (key == KeyboardKey.DownArrow)
            MoveSuggestion(omnibar, 1);
        if (key == KeyboardKey.UpArrow)
            MoveSuggestion(omnibar, -1);
    }

    /// <summary>A match picked from the suggestions, and whether it is to open in a new tab.</summary>
    private readonly record struct PickedMatch(SearchResult? Match, bool InNewTab);

    /// <summary>The best matches of the query under the box, as wide as it, or a note while there are
    /// none yet or none at all; nothing while the box is empty. Returns the match clicked, if any.</summary>
    private static PickedMatch Suggestions(Ui<UiState> ui, Omnibar omnibar, int fieldId)
    {
        var picked = new PickedMatch(null, false);
        if (omnibar.Search.Query.Length == 0)
            return picked;
        if (!ui.TryGetRect(fieldId, out var anchor))
            return picked;
        var theme = ui.Theme;
        var box = theme.PopupSurface(new Box { Width = anchor.Width, Padding = new Spacing(0f, theme.GapTight), Gap = 0f });
        using (ui.Popup(HashCode.Combine(fieldId, SuggestionsSalt), anchor, box, gap: theme.TooltipGap))
        {
            if (!HasSuggestions(omnibar))
            {
                SuggestionNote(ui, omnibar);
                return picked;
            }
            for (var i = 0; i < omnibar.SuggestionCount; i++)
            {
                var row = SuggestionRow(ui, omnibar, omnibar.Search.Rows[i], fieldId);
                if (row.Match != null)
                    picked = row;
            }
        }
        return picked;
    }

    private static bool HasSuggestions(Omnibar omnibar)
    {
        if (!omnibar.Search.ShowsQuery)
            return false;
        return omnibar.SuggestionCount > 0;
    }

    /// <summary>Searching, while the query has not settled or its first matches are still coming; else no matches.</summary>
    private static void SuggestionNote(Ui<UiState> ui, Omnibar omnibar)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var note = IsStillSearching(omnibar.Search) ? t.OmnibarSearching : t.OmnibarNoMatches;
        ui.Text(note, new Box { Height = theme.RowHeight, Padding = new Spacing(theme.ShellPaddingX, 0f), AlignY = Align.Center }, color: theme.TextMuted);
    }

    private static bool IsStillSearching(Search search)
    {
        if (!search.ShowsQuery)
            return true;
        return search.IsBusy;
    }

    /// <summary>A match's icon, name and where it is; a press keeps the box focused, so the click lands.</summary>
    private static PickedMatch SuggestionRow(Ui<UiState> ui, Omnibar omnibar, SearchResult match, int fieldId)
    {
        var theme = ui.Theme;
        var node = match.Node;
        var selected = ReferenceEquals(match, omnibar.SelectedSuggestion);
        var i = ui.Interact(RuntimeHelpers.GetHashCode(node));
        if (i.Pressed)
            ui.Focus(fieldId);
        var nameColor = selected ? theme.SelectionText : theme.Text;
        using (ui.Row(i.Id, SuggestionBox(theme, selected, i.Hovered)))
        {
            NodeIcons.Draw(ui, node);
            ui.Text(match.Text, new Box { Width = Sizing.Grow() with { Priority = 1 } }, color: nameColor, overflow: TextOverflow.Ellipsis);
            ui.Text(match.Location, new Box { Width = Sizing.Shrink() }, color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.EllipsisMiddle);
        }
        if (i.Clicked)
            return new PickedMatch(match, IsCommandHeld(ui));
        if (i.ClickedWith(PointerButton.Middle))
            return new PickedMatch(match, true);
        return new PickedMatch(null, false);
    }

    /// <summary>A row of a list that has focus, as the search box holds it for the suggestions.</summary>
    private static Box SuggestionBox(UiTheme theme, bool selected, bool hovered) => new()
    {
        Height = theme.RowHeight,
        Padding = new Spacing(theme.ShellPaddingX, 0f),
        Gap = theme.GapTight,
        AlignY = Align.Center,
        Background = theme.PickRowBackground(selected, focused: true, hovered),
    };
}
