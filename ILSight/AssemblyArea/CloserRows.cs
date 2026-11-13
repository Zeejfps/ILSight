using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>The rows of the "close assemblies" dialog: every item of the list in the list's order, kept in step
/// with the list while the dialog is up, ticks and the anchor kept for the files still listed; the row last clicked,
/// which a Shift-click's range runs from; and whether the filter box is still to take focus since the dialog
/// opened.</summary>
public sealed class CloserRows
{
    private readonly List<CloseChoice> _order = [];

    private int _listedVersion = -1;

    private CloseChoice? _anchor;
    private bool _isFocusPending;

    /// <summary>The items of the list, in its order.</summary>
    public IReadOnlyList<CloseChoice> Order => _order;

    /// <summary>Lists <paramref name="assemblies"/>' items afresh, with <paramref name="picked"/> ticked when it is
    /// one of them and nothing else, and the filter box to take focus.</summary>
    public void Start(AssemblyList assemblies, LoadedAssembly? picked)
    {
        _isFocusPending = true;
        _anchor = null;
        _order.Clear();
        _listedVersion = -1;
        Update(assemblies);
        if (picked is null)
        {
            return;
        }

        foreach (var choice in _order)
        {
            if (choice.Assembly == picked)
            {
                choice.Included = true;
            }
        }
    }

    public void Clear()
    {
        _order.Clear();
        _anchor = null;
    }

    /// <summary>True once after <see cref="Start"/>: the filter box should take focus.</summary>
    public bool TryTakeFilterFocus()
    {
        var focus = _isFocusPending;
        _isFocusPending = false;
        return focus;
    }

    /// <summary>Lists the items again once the list changed, as a reload or an open does while the dialog
    /// is up, keeping the ticks of the files still listed.</summary>
    public void Update(AssemblyList assemblies)
    {
        if (assemblies.Version == _listedVersion)
        {
            return;
        }

        _listedVersion = assemblies.Version;
        var ticked = new HashSet<AssemblyPath>();
        foreach (var choice in _order)
        {
            if (choice.Included)
            {
                ticked.Add(choice.Assembly.FilePath);
            }
        }
        _order.Clear();
        foreach (var item in assemblies.Listed)
        {
            _order.Add(new CloseChoice { Assembly = item, Included = ticked.Contains(item.FilePath) });
        }

        if (_anchor is not null && !_order.Contains(_anchor))
        {
            _anchor = null;
        }
    }

    /// <summary>Follows a click that set <paramref name="clicked"/>'s tick: with <paramref name="extend"/>
    /// (Shift held), every row in <paramref name="shown"/> from the anchor to it takes the same tick; rows
    /// outside the range keep theirs. Without an anchor among <paramref name="shown"/>, only
    /// <paramref name="clicked"/> changes. Either way it becomes the anchor. <paramref name="shown"/> is the
    /// list as shown before the click, so a row that Selected only hides once unticked still counts.</summary>
    public void TickClicked(IReadOnlyList<CloseChoice> shown, CloseChoice clicked, bool extend)
    {
        var anchor = _anchor;
        _anchor = clicked;
        if (!extend || anchor is null)
        {
            return;
        }

        var from = IndexOf(shown, anchor);
        var to = IndexOf(shown, clicked);
        if (from < 0 || to < 0)
        {
            return;
        }

        for (var i = Math.Min(from, to); i <= Math.Max(from, to); i++)
        {
            shown[i].Included = clicked.Included;
        }
    }

    /// <summary>How many items are ticked, shown or not.</summary>
    public int CountIncluded()
    {
        var count = 0;
        foreach (var choice in _order)
        {
            if (choice.Included)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>The ticked items, shown or not, in the list's order: what Close closes.</summary>
    public List<LoadedAssembly> ListToClose()
    {
        var items = new List<LoadedAssembly>();
        foreach (var choice in _order)
        {
            if (choice.Included)
            {
                items.Add(choice.Assembly);
            }
        }
        return items;
    }

    private static int IndexOf(IReadOnlyList<CloseChoice> shown, CloseChoice choice)
    {
        for (var i = 0; i < shown.Count; i++)
        {
            if (shown[i] == choice)
            {
                return i;
            }
        }
        return -1;
    }
}
