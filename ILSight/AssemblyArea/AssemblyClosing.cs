using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>What the "close assemblies" dialog does: listing the items, filtering and ticking them, and
/// what Close closes.</summary>
public static class AssemblyClosing
{
    /// <summary>Opens the dialog on <paramref name="assemblies"/>' items, none filtered out, with
    /// <paramref name="picked"/>, the assembly of the node selected in the tree, ticked when it is an item of
    /// the list; one read out of a package ticks nothing, so that its package is never closed unasked.</summary>
    public static void OpenCloser(AssemblyCloser closer, AssemblyList assemblies, LoadedAssembly? picked)
    {
        closer.IsOpen = true;
        closer.FocusFilter = true;
        closer.Filter = "";
        closer.SelectedOnly = false;
        closer.Anchor = null;
        closer.Order.Clear();
        closer.ListedVersion = -1;
        Update(closer, assemblies);
        if (picked is null)
        {
            return;
        }

        foreach (var choice in closer.Order)
        {
            if (choice.Assembly == picked)
            {
                choice.Included = true;
            }
        }
    }

    public static void CloseCloser(AssemblyCloser closer)
    {
        closer.IsOpen = false;
        closer.Order.Clear();
        closer.Anchor = null;
    }

    /// <summary>True once after <see cref="OpenCloser"/>: the filter box should take focus.</summary>
    public static bool TryTakeFilterFocus(AssemblyCloser closer)
    {
        var focus = closer.FocusFilter;
        closer.FocusFilter = false;
        return focus;
    }

    /// <summary>Lists the items again once the list changed, as a reload or an open does while the dialog
    /// is up, keeping the ticks of the files still listed.</summary>
    public static void Update(AssemblyCloser closer, AssemblyList assemblies)
    {
        if (assemblies.Version == closer.ListedVersion)
        {
            return;
        }

        closer.ListedVersion = assemblies.Version;
        var ticked = new HashSet<AssemblyPath>();
        foreach (var choice in closer.Order)
        {
            if (choice.Included)
            {
                ticked.Add(choice.Assembly.FilePath);
            }
        }
        closer.Order.Clear();
        foreach (var item in assemblies.Listed)
        {
            closer.Order.Add(new CloseChoice { Assembly = item, Included = ticked.Contains(item.FilePath) });
        }

        if (closer.Anchor is not null && !closer.Order.Contains(closer.Anchor))
        {
            closer.Anchor = null;
        }
    }

    /// <summary>The items the dialog shows, in the list's order. Runs each frame the dialog shows, so it
    /// is a loop: a lambda here would capture the closer.</summary>
    public static List<CloseChoice> ListShown(AssemblyCloser closer)
    {
        var shown = new List<CloseChoice>();
        foreach (var choice in closer.Order)
        {
            if (IsShown(closer, choice))
            {
                shown.Add(choice);
            }
        }
        return shown;
    }

    private static bool IsShown(AssemblyCloser closer, CloseChoice choice)
    {
        if (closer.SelectedOnly && !choice.Included)
        {
            return false;
        }

        var filter = closer.Filter.Trim();
        if (filter.Length == 0)
        {
            return true;
        }

        if (choice.Assembly.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return choice.Assembly.FilePath.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Shows every item again.</summary>
    public static void ClearFilter(AssemblyCloser closer)
    {
        closer.Filter = "";
        closer.SelectedOnly = false;
    }

    /// <summary>Whether every shown item in <paramref name="shown"/> is ticked, none is, or some are (null).</summary>
    public static bool? CalculateAllShownIncluded(IReadOnlyList<CloseChoice> shown)
    {
        var count = 0;
        foreach (var choice in shown)
        {
            if (choice.Included)
            {
                count++;
            }
        }
        if (count == 0)
        {
            return false;
        }

        if (count == shown.Count)
        {
            return true;
        }

        return null;
    }

    /// <summary>Ticks every item of <paramref name="shown"/> when <paramref name="all"/>, else unticks them;
    /// the hidden ones stay as they are.</summary>
    public static void IncludeShown(IReadOnlyList<CloseChoice> shown, bool all)
    {
        foreach (var choice in shown)
        {
            choice.Included = all;
        }
    }

    /// <summary>Follows a click that set <paramref name="clicked"/>'s tick: with <paramref name="extend"/>
    /// (Shift held), every row in <paramref name="shown"/> from the anchor to it takes the same tick; rows
    /// outside the range keep theirs. Without an anchor among <paramref name="shown"/>, only
    /// <paramref name="clicked"/> changes. Either way it becomes the anchor. <paramref name="shown"/> is the
    /// list as shown before the click, so a row that Selected only hides once unticked still counts.</summary>
    public static void TickClicked(AssemblyCloser closer, IReadOnlyList<CloseChoice> shown, CloseChoice clicked, bool extend)
    {
        var anchor = closer.Anchor;
        closer.Anchor = clicked;
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

    /// <summary>How many items are ticked, shown or not.</summary>
    public static int CountIncluded(AssemblyCloser closer)
    {
        var count = 0;
        foreach (var choice in closer.Order)
        {
            if (choice.Included)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>The ticked items, shown or not, in the list's order: what Close closes.</summary>
    public static List<LoadedAssembly> ListToClose(AssemblyCloser closer)
    {
        var items = new List<LoadedAssembly>();
        foreach (var choice in closer.Order)
        {
            if (choice.Included)
            {
                items.Add(choice.Assembly);
            }
        }
        return items;
    }
}
