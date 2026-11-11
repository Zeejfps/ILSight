using ILSight.Core;

namespace ILSight;

/// <summary>The bar over each document, one for all of them: which document's bar searches instead of
/// showing its path, the best matches it suggests, and the list of a crumb's children open under it.
/// Starting a search closes the list; one bar at most searches, and one list at most is open.</summary>
public sealed class Omnibar
{
    /// <summary>How many of the best matches the search suggests.</summary>
    public const int MaxSuggestions = 10;

    /// <summary>The search a bar runs, or null while every bar shows its path.</summary>
    private OmnibarSearch? _searching;

    /// <summary>The query typed into the bar and its matches, the closest first.</summary>
    public required Search Search { get; init; }

    /// <summary>The document panel whose bar searches, or null while every bar shows its path.</summary>
    public string? SearchingPanel => _searching?.Panel;

    /// <summary>The children of a crumb, open under it, or null while no list is open.</summary>
    public CrumbMenu? Menu { get; private set; }

    public bool IsSearching => _searching is not null;

    /// <summary>How many matches are suggested.</summary>
    public int SuggestionCount => Math.Min(Search.Rows.Count, MaxSuggestions);

    /// <summary>Whether the matches of the query typed are in, and some of them are suggested.</summary>
    public bool HasSuggestions => Search.ShowsQuery && SuggestionCount > 0;

    /// <summary>Whether the query has not settled yet or its first matches are still coming.</summary>
    public bool IsStillSearching => !Search.ShowsQuery || Search.IsBusy;

    /// <summary>The suggestion selected, or null when none of those suggested is.</summary>
    public SearchResult? SelectedSuggestion
    {
        get
        {
            var index = ItemSelections.FindIndexIn(Search.Selection, Search.Rows);
            if (index is null)
            {
                return null;
            }

            if (index.Value >= SuggestionCount)
            {
                return null;
            }

            return Search.Rows[index.Value];
        }
    }

    public bool IsSearchingIn(string panel)
    {
        return SearchingPanel == panel;
    }

    /// <summary>Has the bar of <paramref name="panel"/> search, from an empty query, its box taking focus,
    /// and closes the list of children open in any bar.</summary>
    public void StartSearch(string panel)
    {
        _searching = new OmnibarSearch { Panel = panel };
        Searches.Clear(Search);
        Menu = null;
    }

    /// <summary>Has every bar show its path again.</summary>
    public void StopSearch()
    {
        _searching = null;
        Searches.Clear(Search);
    }

    /// <summary>True once after <see cref="StartSearch"/>: the search box is to take focus now.</summary>
    public bool TryTakeFocus()
    {
        var searching = _searching;
        if (searching is null || !searching.IsFocusPending)
        {
            return false;
        }

        searching.IsFocusPending = false;
        return true;
    }

    /// <summary>The suggestion to go to: the one selected, else the best match; null when none is suggested.</summary>
    public SearchResult? FindSuggestionToShow()
    {
        var selected = SelectedSuggestion;
        if (selected != null)
        {
            return selected;
        }

        if (SuggestionCount == 0)
        {
            return null;
        }

        return Search.Rows[0];
    }

    /// <summary>Selects the suggestion <paramref name="by"/> rows on from the selected one, kept within those suggested.</summary>
    public void MoveSuggestion(int by)
    {
        if (SuggestionCount == 0)
        {
            return;
        }

        Searches.Move(Search, by);
        var index = ItemSelections.FindIndexIn(Search.Selection, Search.Rows);
        if (index is null)
        {
            return;
        }

        if (index.Value < SuggestionCount)
        {
            return;
        }

        Searches.Select(Search, Search.Rows[SuggestionCount - 1]);
    }

    /// <summary>Whether the list of <paramref name="node"/>'s children is open in the bar of <paramref name="panel"/>.</summary>
    public bool IsMenuOpenAt(string panel, BrowseNode node)
    {
        var menu = Menu;
        if (menu is null)
        {
            return false;
        }

        if (menu.Panel != panel)
        {
            return false;
        }

        return ReferenceEquals(menu.Node, node);
    }

    /// <summary>Lists the children of <paramref name="node"/> in the bar of <paramref name="panel"/>, with
    /// <paramref name="next"/>, the crumb after it, to be selected.</summary>
    public void OpenMenu(string panel, BrowseNode node, BrowseNode? next)
    {
        Menu = new CrumbMenu { Panel = panel, Node = node, ToSelect = next };
    }

    /// <summary>Closes the list of the children of the crumb at <paramref name="index"/> of <paramref name="crumbs"/>
    /// in the bar of <paramref name="panel"/> while it is open there; else opens it, the crumb after it to be selected.</summary>
    public void ToggleMenu(string panel, IReadOnlyList<Crumb> crumbs, int index)
    {
        var node = crumbs[index].Node;
        if (IsMenuOpenAt(panel, node))
        {
            Menu = null;
            return;
        }
        OpenMenu(panel, node, FindNextNode(crumbs, index));
    }

    private static BrowseNode? FindNextNode(IReadOnlyList<Crumb> crumbs, int index)
    {
        var next = index + 1;
        if (next >= crumbs.Count)
        {
            return null;
        }

        return crumbs[next].Node;
    }

    public void CloseMenu()
    {
        Menu = null;
    }

    /// <summary>Closes the list of children if it is open in the bar of <paramref name="panel"/>.</summary>
    public void CloseMenuIn(string panel)
    {
        var menu = Menu;
        if (menu is null)
        {
            return;
        }

        if (menu.Panel == panel)
        {
            Menu = null;
        }
    }

    /// <summary>The search one bar runs, and whether its box is yet to take focus.</summary>
    private sealed class OmnibarSearch
    {
        public required string Panel { get; init; }

        public bool IsFocusPending = true;
    }
}
