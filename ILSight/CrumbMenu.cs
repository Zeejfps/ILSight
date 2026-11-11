using ILSight.Core;

namespace ILSight;

/// <summary>The children of one crumb of a document's omnibar, listed under it while it is open: those
/// whose name holds the filter typed, the crumb after it on the path selected once they are built. Opening,
/// it lists them unfiltered and its filter takes focus.</summary>
public sealed class CrumbMenu
{
    /// <summary>The children listed, by what they were filtered from and by.</summary>
    private Memo<(ByReference<IReadOnlyList<BrowseNode>>, string), IReadOnlyList<BrowseNode>> _listed;

    private BrowseNode? _toSelect;
    private bool _isFocusPending = true;

    /// <summary>The children listed, as last filtered.</summary>
    public IReadOnlyList<BrowseNode> Shown => _listed.Last ?? [];

    /// <summary>The crumb to select once the children built hold it; null once selected, or when there is none.</summary>
    public required BrowseNode? ToSelect
    {
        get => _toSelect;
        init => _toSelect = value;
    }

    /// <summary>What a child's name must hold to be listed, ignoring case. A field, for the filter's box to edit.</summary>
    public string Filter = "";

    /// <summary>The document panel whose bar the list is open in.</summary>
    public required string Panel { get; init; }

    /// <summary>The crumb whose children are listed.</summary>
    public required BrowseNode Node { get; init; }

    public ItemSelection<BrowseNode> Selection { get; } = new();

    /// <summary>True once after the list opened: the filter is to take focus now.</summary>
    public bool TryTakeFocus()
    {
        var focus = _isFocusPending;
        _isFocusPending = false;
        return focus;
    }

    /// <summary>The <paramref name="children"/> whose name, as <paramref name="tree"/> labels their rows, holds
    /// the filter; filtered again only when the children or the filter changed. Runs each frame the menu is
    /// open, so what filters is a function of its own: a lambda here would allocate its closure at the
    /// top of this function, on the way to returning what is already known.</summary>
    public IReadOnlyList<BrowseNode> ListShown(IReadOnlyList<BrowseNode> children, BrowseTree tree)
    {
        if (_listed.TryGet((new ByReference<IReadOnlyList<BrowseNode>>(children), Filter), out var shown))
        {
            return shown;
        }

        FilterChildren(children, tree);
        return Shown;
    }

    /// <summary>Selects the child <paramref name="by"/> rows on from the selected one, among those listed.</summary>
    public void MoveSelection(int by)
    {
        ItemSelections.Move(Selection, by, Shown);
    }

    private void FilterChildren(IReadOnlyList<BrowseNode> children, BrowseTree tree)
    {
        var shown = children.Where(c => HoldsFilter(Trees.GetRowLabel(tree, c))).ToList();
        _listed = new Memo<(ByReference<IReadOnlyList<BrowseNode>>, string), IReadOnlyList<BrowseNode>>((new ByReference<IReadOnlyList<BrowseNode>>(children), Filter), shown);
        SelectWaitingChild();
        ItemSelections.DropIfGone(Selection, Shown);
        SelectFirstMatch();
    }

    private bool HoldsFilter(string name)
    {
        return name.Contains(Filter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Selects the crumb after this one once the children built hold it.</summary>
    private void SelectWaitingChild()
    {
        var toSelect = _toSelect;
        if (toSelect is null)
        {
            return;
        }

        if (!Shown.Contains(toSelect))
        {
            return;
        }

        ItemSelections.Select(Selection, toSelect, Shown);
        _toSelect = null;
    }

    /// <summary>Selects the first child listed while a filter leaves none selected, for Enter to go to.</summary>
    private void SelectFirstMatch()
    {
        if (Filter.Length == 0)
        {
            return;
        }

        if (Selection.Item is not null)
        {
            return;
        }

        if (Shown.Count == 0)
        {
            return;
        }

        ItemSelections.Select(Selection, Shown[0], Shown);
    }
}
