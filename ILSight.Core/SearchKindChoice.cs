namespace ILSight.Core;

/// <summary>Picking the <see cref="SearchKinds"/> to search, one kind at a time, beside a choice of all of
/// them. While all are picked no single kind counts as checked: checking one then picks it alone, and
/// unchecking the last one picked goes back to all.</summary>
public static class SearchKindChoice
{
    /// <summary>Whether <paramref name="kind"/> shows as checked when <paramref name="kinds"/> are picked;
    /// none does while all are.</summary>
    public static bool IsChecked(SearchKinds kinds, SearchKinds kind)
    {
        return kinds != SearchKinds.All && (kinds & kind) != 0;
    }

    /// <summary>What is picked once <paramref name="kind"/> is checked (<paramref name="on"/>) or unchecked
    /// while <paramref name="kinds"/> are picked.</summary>
    public static SearchKinds Toggle(SearchKinds kinds, SearchKinds kind, bool on)
    {
        if (kinds == SearchKinds.All)
        {
            return kind;
        }

        if (on)
        {
            return kinds | kind;
        }

        var left = kinds & ~kind;
        if (left == SearchKinds.None)
        {
            return SearchKinds.All;
        }

        return left;
    }
}
