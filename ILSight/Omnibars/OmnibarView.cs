using System.Globalization;
using System.Runtime.CompilerServices;
using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.Commanding;
using ILSight.Diagramming;

namespace ILSight.Omnibars;

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

    /// <summary>The bar over <paramref name="tab"/>. A crumb, child or match picked shows in
    /// <paramref name="navigation"/>'s tabs in this frame, in <paramref name="language"/> with
    /// <paramref name="options"/>, in the preview tab while <paramref name="previewTabs"/>; only its decompile is
    /// queued. A comparison's tools compare <paramref name="assemblies"/> as they are open.</summary>
    public static void Render(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies, DecompilationLanguage language, DecompilerOptions options,
        bool previewTabs, DocumentTab tab)
    {
        if (!ui.State.Display.ShowOmnibar)
        {
            return;
        }

        var panel = DocumentPanels.GetPanel(ui.State.Documents, tab);
        if (ContinueSearch(ui, panel))
        {
            SearchBar(ui, navigation, language, options, previewTabs);
        }
        else
        {
            PathBar(ui, navigation, assemblies, language, options, previewTabs, tab, panel);
        }

        ui.Separator();
    }

    /// <summary>Whether the bar of <paramref name="panel"/> searches: its box takes focus when the search
    /// just started, and the search stops once the box lost focus.</summary>
    private static bool ContinueSearch(Ui<UiState> ui, string panel)
    {
        var omnibar = ui.State.Omnibar;
        if (!omnibar.IsSearchingIn(panel))
        {
            return false;
        }

        var fieldId = ui.GetId(SearchFieldId);
        if (omnibar.TryTakeFocus())
        {
            ui.Focus(fieldId);
            return true;
        }
        if (ui.HasFocus(fieldId))
        {
            return true;
        }

        omnibar.StopSearch();
        return false;
    }

    private static void PathBar(Ui<UiState> ui, Navigation navigation, AssemblyList assemblies, DecompilationLanguage language, DecompilerOptions options,
        bool previewTabs, DocumentTab tab, string panel)
    {
        var view = DocumentPanels.GetView(ui.State.Documents, tab);
        view.Breadcrumb.Follow(tab.Node, ui.State.Tree);
        var crumbs = view.Breadcrumb.Crumbs;
        var menuOwner = 0;
        using (ui.PaneToolbar(new Box { Gap = 0f }))
        {
            for (var i = 0; i < crumbs.Count; i++)
            {
                using (ui.Id(i))
                {
                    CrumbButton(ui, navigation, language, options, previewTabs, crumbs[i], i);
                    var owner = ChildrenButton(ui, panel, crumbs, i);
                    if (owner != 0)
                    {
                        menuOwner = owner;
                    }
                }
            }
            ContentTools(ui, assemblies, tab);
            SearchArea(ui, panel);
            SearchButton(ui, panel);
        }
        ChildrenMenu(ui, navigation, language, options, previewTabs, panel, menuOwner);
    }

    /// <summary>The tools of what the tab shows that no node stands for: a diagram's or a comparison's.</summary>
    private static void ContentTools(Ui<UiState> ui, AssemblyList assemblies, DocumentTab tab)
    {
        switch (tab.Content)
        {
            case TabContent.Diagram:
                DiagramView.Tools(ui, tab);
                break;
            case TabContent.Comparison comparison:
                ComparisonView.Tools(ui, assemblies, tab, comparison.Assemblies);
                break;
        }
    }

    /// <summary>The node's icon and name, which gives up room, earlier crumbs first, while the path is
    /// too long for the bar.</summary>
    private static void CrumbButton(Ui<UiState> ui, Navigation navigation, DecompilationLanguage language, DecompilerOptions options, bool previewTabs,
        Crumb crumb, int depth)
    {
        var theme = ui.Theme;
        var node = crumb.Node;
        var i = ui.Interact("crumb");
        RecordDepthPath(ui, i.Id, depth, "crumb");
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
        {
            Commands.DecompileLater(ui.State, PickNode(navigation, node, IsCommandHeld(ui), language, options, previewTabs));
        }

        if (i.ClickedWith(PointerButton.Middle))
        {
            Commands.DecompileLater(ui.State, navigation.ShowNode(node, language, options, previewTabs, inNewTab: true));
        }
    }

    /// <summary>Has tests find a crumb's <paramref name="name"/>d part by the crumb's depth, as <c>#1/crumb</c>, since
    /// two crumbs can read the same, such as an assembly and its namespace of one name.</summary>
    private static void RecordDepthPath(Ui<UiState> ui, int id, int depth, string name)
    {
        if (!ui.WantsSemantics)
        {
            return;
        }

        ui.RecordTestPath(id, new TestPath([new TestSegment.Keyed(depth.ToString(CultureInfo.InvariantCulture)), new TestSegment.Named(name)]));
    }

    /// <summary>Public API in the text color, the rest muted, as the tree shows them.</summary>
    private static uint CrumbColor(UiTheme theme, BrowseNode node)
    {
        var visibility = NodeDefinitions.GetVisibility(node.Subject);
        if (visibility.IsPublicApi)
        {
            return theme.Text;
        }

        return theme.TextMuted;
    }

    /// <summary>Shows <paramref name="node"/> as one picked in the tree is, or in a new tab when <paramref name="inNewTab"/>.
    /// Returns the load it started in the tab; null when it started none.</summary>
    private static DecompileTicket? PickNode(Navigation navigation, BrowseNode node, bool inNewTab, DecompilationLanguage language, DecompilerOptions options,
        bool previewTabs)
    {
        if (inNewTab)
        {
            return navigation.ShowNode(node, language, options, previewTabs, inNewTab: true);
        }
        return navigation.PreviewNode(node, language, options, previewTabs);
    }

    private static bool IsCommandHeld(Ui<UiState> ui)
    {
        return (ui.Input.Modifiers & Shortcut.CommandModifier) != 0;
    }

    /// <summary>The arrow after a crumb that has children, which opens and closes the list of them, pointing
    /// down while it is open. Returns the arrow's id while its list is open, else 0.</summary>
    private static int ChildrenButton(Ui<UiState> ui, string panel, IReadOnlyList<Crumb> crumbs, int index)
    {
        var node = crumbs[index].Node;
        if (!ui.State.Tree.HasChildren(node))
        {
            return 0;
        }

        var theme = ui.Theme;
        var omnibar = ui.State.Omnibar;
        var i = ui.Interact("children");
        RecordDepthPath(ui, i.Id, index, "children");
        if (i.Clicked)
        {
            omnibar.ToggleMenu(panel, crumbs, index);
        }

        var open = omnibar.IsMenuOpenAt(panel, node);
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
        {
            ui.Tooltip(i.Id, ui.Memo("contents-tip", (crumbs[index].Label, ui.State.Strings), static a => a.Strings.OmnibarContents(a.Label)));
        }

        if (!open)
        {
            return 0;
        }

        return i.Id;
    }

    private static UiGlyph ArrowOf(bool open)
    {
        return open ? UiGlyph.DisclosureOpen : UiGlyph.DisclosureClosed;
    }

    /// <summary>The room left after the path, which starts a search when clicked.</summary>
    private static void SearchArea(Ui<UiState> ui, string panel)
    {
        var i = ui.Interact("search-area");
        if (i.Hovered)
        {
            ui.SetCursor(MouseCursor.Text);
        }

        ui.Leaf(i.Id, new Box { Width = Sizing.Grow(), Height = Sizing.Grow() });
        if (i.Clicked)
        {
            ui.State.Omnibar.StartSearch(panel);
        }
    }

    private static void SearchButton(Ui<UiState> ui, string panel)
    {
        var t = ui.State.Strings;
        var tooltip = ui.Memo("omnibar-search-tip", t, static s => s.SearchFieldTooltip(Shortcuts.OmnibarSearch));
        if (ui.IconButton("search", ui.State.Icons.SearchField, tooltip))
        {
            ui.State.Omnibar.StartSearch(panel);
        }
    }

    /// <summary>The list of the open crumb's children under its arrow <paramref name="owner"/>, with a
    /// filter over them; 0 when no list is open in this bar, which forgets one left open on a crumb gone
    /// from it.</summary>
    private static void ChildrenMenu(Ui<UiState> ui, Navigation navigation, DecompilationLanguage language, DecompilerOptions options, bool previewTabs,
        string panel, int owner)
    {
        var omnibar = ui.State.Omnibar;
        var menu = omnibar.Menu;
        if (menu is null)
        {
            return;
        }

        if (owner == 0)
        {
            omnibar.CloseMenuIn(panel);
            return;
        }
        var theme = ui.Theme;
        var isOpen = true;
        using var flyout = ui.Flyout(HashCode.Combine(owner, MenuSalt), ref isOpen, owner, new Box { Width = MenuWidth, Gap = theme.GapTight });
        if (!isOpen)
        {
            omnibar.CloseMenu();
        }

        if (!flyout.IsOpen)
        {
            return;
        }

        var children = ui.State.Tree.GetChildren(menu.Node);
        if (children.PlaceholderRow is TreeRow placeholder)
        {
            ui.Text(placeholder.Label, color: theme.TextMuted);
            return;
        }
        var shown = menu.ListShown(children.Nodes, ui.State.Tree);
        var listId = HashCode.Combine(owner, ListSalt);
        var submitted = Filter(ui, menu, shown);
        var picked = ChildRows(ui, menu, shown, listId);
        if (submitted)
        {
            picked = new PickedChild(menu.Selection.Item, IsCommandHeld(ui));
        }

        var node = picked.Node;
        if (node is null)
        {
            return;
        }

        omnibar.CloseMenu();
        Commands.DecompileLater(ui.State, PickNode(navigation, node, picked.InNewTab, language, options, previewTabs));
    }

    /// <summary>The filter, which takes focus as the list opens and walks the list with Up and Down.
    /// Returns whether Enter was pressed in it.</summary>
    private static bool Filter(Ui<UiState> ui, CrumbMenu menu, IReadOnlyList<BrowseNode> shown)
    {
        var fieldId = ui.GetId(FilterFieldId);
        if (menu.TryTakeFocus())
        {
            ui.Focus(fieldId);
        }

        var submitted = ui.TextField(FilterFieldId, ref menu.Filter, ui.State.Strings.OmnibarFilter, Sizing.Grow(), clearable: true);
        if (!ui.HasFocus(fieldId))
        {
            return false;
        }

        foreach (var stroke in ui.Input.Keys)
        {
            MoveSelection(menu, stroke.Key);
        }

        return submitted;
    }

    private static void MoveSelection(CrumbMenu menu, KeyboardKey key)
    {
        if (key == KeyboardKey.DownArrow)
        {
            menu.MoveSelection(1);
        }

        if (key == KeyboardKey.UpArrow)
        {
            menu.MoveSelection(-1);
        }
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
                if (row.Node is not null)
                {
                    picked = row;
                }
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
        var label = Trees.GetRowLabel(ui.State.Tree, child);
        NodeIcons.Draw(ui, child);
        ui.Text(label, new Box { Width = Sizing.Grow() }, color: color, overflow: TextOverflow.Ellipsis);
        RecordChildPath(ui, row.Interaction.Id, label);
        if (row.Interaction.Clicked)
        {
            return new PickedChild(child, IsCommandHeld(ui));
        }

        if (row.Interaction.ClickedWith(PointerButton.Middle))
        {
            return new PickedChild(child, true);
        }

        return new PickedChild(null, false);
    }

    /// <summary>Has tests find a child's row by its label, as <c>child/#Greeter</c>.</summary>
    private static void RecordChildPath(Ui<UiState> ui, int id, string label)
    {
        if (!ui.WantsSemantics)
        {
            return;
        }

        ui.RecordTestPath(id, new TestPath([new TestSegment.Named("child"), new TestSegment.Keyed(label)]));
    }

    /// <summary>The search box, whose Enter goes to the selected match, else the best, and whose Up and
    /// Down walk the matches suggested under it.</summary>
    private static void SearchBar(Ui<UiState> ui, Navigation navigation, DecompilationLanguage language, DecompilerOptions options, bool previewTabs)
    {
        var t = ui.State.Strings;
        var omnibar = ui.State.Omnibar;
        var fieldId = ui.GetId(SearchFieldId);
        bool submitted;
        using (ui.PaneToolbar())
        {
            submitted = ui.TextField(SearchFieldId, ref omnibar.Search.Query, t.OmnibarSearch, Sizing.Grow(), ui.State.Icons.SearchField, clearable: true);
            SyntaxTooltip(ui, omnibar, fieldId);
        }
        WalkSuggestions(ui, omnibar, fieldId);
        var clicked = Suggestions(ui, omnibar, fieldId);
        if (submitted)
        {
            clicked = new PickedMatch(omnibar.FindSuggestionToShow(), IsCommandHeld(ui));
        }

        var match = clicked.Match;
        if (match == null)
        {
            return;
        }

        omnibar.StopSearch();
        Commands.DecompileLater(ui.State, navigation.ShowNode(match.Node, language, options, previewTabs, clicked.InNewTab));
    }

    /// <summary>The query syntax, over the box while it is empty, so it never covers the suggestions.</summary>
    private static void SyntaxTooltip(Ui<UiState> ui, Omnibar omnibar, int fieldId)
    {
        if (omnibar.Search.Query.Length > 0)
        {
            return;
        }

        var t = ui.State.Strings;
        ui.Tooltip(fieldId, ui.Memo("omnibar-syntax", t, static s => s.SearchSyntaxTooltip(Shortcuts.OmnibarSearch)));
    }

    private static void WalkSuggestions(Ui<UiState> ui, Omnibar omnibar, int fieldId)
    {
        if (!ui.HasFocus(fieldId))
        {
            return;
        }

        foreach (var stroke in ui.Input.Keys)
        {
            MoveSuggestion(omnibar, stroke.Key);
        }
    }

    private static void MoveSuggestion(Omnibar omnibar, KeyboardKey key)
    {
        if (key == KeyboardKey.DownArrow)
        {
            omnibar.MoveSuggestion(1);
        }

        if (key == KeyboardKey.UpArrow)
        {
            omnibar.MoveSuggestion(-1);
        }
    }

    /// <summary>A match picked from the suggestions, and whether it is to open in a new tab.</summary>
    private readonly record struct PickedMatch(SearchResult? Match, bool InNewTab);

    /// <summary>The best matches of the query under the box, as wide as it, or a note while there are
    /// none yet or none at all; nothing while the box is empty. Returns the match clicked, if any.</summary>
    private static PickedMatch Suggestions(Ui<UiState> ui, Omnibar omnibar, int fieldId)
    {
        var picked = new PickedMatch(null, false);
        if (omnibar.Search.Query.Length == 0)
        {
            return picked;
        }

        if (!ui.TryGetRect(fieldId, out var anchor))
        {
            return picked;
        }

        var theme = ui.Theme;
        var box = theme.PopupSurface(new Box { Width = anchor.Width, Padding = new Spacing(0f, theme.GapTight), Gap = 0f });
        using (ui.Popup(HashCode.Combine(fieldId, SuggestionsSalt), anchor, box, gap: theme.TooltipGap))
        {
            if (!omnibar.HasSuggestions)
            {
                SuggestionNote(ui, omnibar);
                return picked;
            }
            for (var i = 0; i < omnibar.SuggestionCount; i++)
            {
                var row = SuggestionRow(ui, omnibar, omnibar.Search.Rows[i], fieldId);
                if (row.Match != null)
                {
                    picked = row;
                }
            }
        }
        return picked;
    }

    /// <summary>Searching, while the query has not settled or its first matches are still coming; else no matches.</summary>
    private static void SuggestionNote(Ui<UiState> ui, Omnibar omnibar)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var note = omnibar.IsStillSearching ? t.OmnibarSearching : t.OmnibarNoMatches;
        ui.Text(note, new Box { Height = theme.RowHeight, Padding = new Spacing(theme.ShellPaddingX, 0f), AlignY = Align.Center }, color: theme.TextMuted);
    }

    /// <summary>A match's icon, name and where it is; a press keeps the box focused, so the click lands.</summary>
    private static PickedMatch SuggestionRow(Ui<UiState> ui, Omnibar omnibar, SearchResult match, int fieldId)
    {
        var theme = ui.Theme;
        var node = match.Node;
        var selected = ReferenceEquals(match, omnibar.SelectedSuggestion);
        var i = ui.Interact(RuntimeHelpers.GetHashCode(node));
        if (i.Pressed)
        {
            ui.Focus(fieldId);
        }

        var nameColor = selected ? theme.SelectionText : theme.Text;
        using (ui.Row(i.Id, SuggestionBox(theme, selected, i.Hovered)))
        {
            DescribeSuggestion(ui, i.Id, match.Text, selected);
            NodeIcons.Draw(ui, node);
            ui.Text(match.Text, new Box { Width = Sizing.Grow() with { Priority = 1 } }, color: nameColor, overflow: TextOverflow.Ellipsis);
            ui.Text(match.Location, new Box { Width = Sizing.Shrink() }, color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.EllipsisMiddle);
        }
        if (i.Clicked)
        {
            return new PickedMatch(match, IsCommandHeld(ui));
        }

        if (i.ClickedWith(PointerButton.Middle))
        {
            return new PickedMatch(match, true);
        }

        return new PickedMatch(null, false);
    }

    /// <summary>Has tests find a suggestion by its text, as <c>suggestion/#Farewell</c>, as a list item that is
    /// selected while it is the one Enter goes to.</summary>
    private static void DescribeSuggestion(Ui<UiState> ui, int id, string text, bool selected)
    {
        if (!ui.WantsSemantics)
        {
            return;
        }

        ui.RecordTestPath(id, new TestPath([new TestSegment.Named("suggestion"), new TestSegment.Keyed(text)]));
        ui.Semantics(SemanticRole.ListItem, text, selected ? SemanticStates.Selected : SemanticStates.None);
    }

    /// <summary>A row of a list that has focus, as the search box holds it for the suggestions.</summary>
    private static Box SuggestionBox(UiTheme theme, bool selected, bool hovered)
    {
        return new Box()
        {
            Height = theme.RowHeight,
            Padding = new Spacing(theme.ShellPaddingX, 0f),
            Gap = theme.GapTight,
            AlignY = Align.Center,
            Background = theme.PickRowBackground(selected, focused: true, hovered),
        };
    }
}
