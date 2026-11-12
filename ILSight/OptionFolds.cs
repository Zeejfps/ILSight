namespace ILSight;

public static class OptionFolds
{
    public static bool IsFolded(FoldedGroups groups, string group)
    {
        return groups.Folded.Contains(group);
    }

    public static void Fold(FoldedGroups groups, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            if (groups.Folded.Add(name))
            {
                groups.Revision++;
            }
        }
    }

    /// <summary>Unfolds <paramref name="group"/> when it is folded, and folds it otherwise.</summary>
    public static void Flip(FoldedGroups groups, string group)
    {
        if (!groups.Folded.Remove(group))
        {
            groups.Folded.Add(group);
        }

        groups.Revision++;
    }

    public static void UnfoldAll(FoldedGroups groups)
    {
        if (groups.Folded.Count == 0)
        {
            return;
        }

        groups.Folded.Clear();
        groups.Revision++;
    }

    /// <summary>The folded groups in order, to save.</summary>
    public static List<string> ListInOrder(FoldedGroups groups)
    {
        return groups.Folded
            .Order()
            .ToList();
    }
}
