namespace ILSight.AssemblyArea;

/// <summary>What the "close assemblies" dialog shows: the items the filter lets through, and ticking them all.</summary>
public static class AssemblyClosing
{
    /// <summary>The items the dialog shows, in the list's order. Runs each frame the dialog shows, so it
    /// is a loop: a lambda here would capture the closer.</summary>
    public static List<CloseChoice> ListShown(AssemblyCloser closer)
    {
        var shown = new List<CloseChoice>();
        var order = closer.Rows.Order;
        for (var i = 0; i < order.Count; i++)
        {
            var choice = order[i];
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
}
