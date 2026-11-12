namespace ILSight;

/// <summary>Moving what an <see cref="ItemSelection{T}"/> selects.</summary>
public static class ItemSelections
{
    /// <summary>Selects <paramref name="item"/> of <paramref name="items"/> and, unless told otherwise,
    /// scrolls it into view.</summary>
    public static void Select<T>(ItemSelection<T> selection, T item, IReadOnlyList<T> items, bool reveal = true)
        where T : class
    {
        selection.Item = item;
        SelectRow(selection, items, reveal);
    }

    public static void Clear<T>(ItemSelection<T> selection)
        where T : class
    {
        selection.Item = null;
        selection.Rows.Clear();
    }

    /// <summary>Selects the item <paramref name="by"/> rows on from the selected one, kept within
    /// <paramref name="items"/>; with nothing selected, a move back selects the first.</summary>
    public static void Move<T>(ItemSelection<T> selection, int by, IReadOnlyList<T> items)
        where T : class
    {
        ShowIn(selection, items);
        selection.Rows.Move(by, items.Count);
        TakeFrom(selection, items);
    }

    /// <summary>Scrolls the selected item into view the next time the list is recorded.</summary>
    public static void Reveal<T>(ItemSelection<T> selection, IReadOnlyList<T> items)
        where T : class
    {
        if (selection.Item is null)
        {
            return;
        }

        Select(selection, selection.Item, items);
    }

    /// <summary>Drops the selection when the selected item is no longer one of <paramref name="items"/>.</summary>
    public static void DropIfGone<T>(ItemSelection<T> selection, IReadOnlyList<T> items)
        where T : class
    {
        var index = FindIndexIn(selection, items);
        if (index is not null)
        {
            return;
        }

        Clear(selection);
    }

    /// <summary>Gives <see cref="ItemSelection{T}.Rows"/> the selected item's row in <paramref name="items"/>, keeping a
    /// reveal already asked for; call it before the list widget records.</summary>
    public static void ShowIn<T>(ItemSelection<T> selection, IReadOnlyList<T> items)
        where T : class
    {
        DropIfGone(selection, items);
        SelectRow(selection, items, reveal: false);
    }

    /// <summary>Gives <see cref="ItemSelection{T}.Rows"/> the selected item's row in <paramref name="items"/>,
    /// or no row when it is not one of them.</summary>
    private static void SelectRow<T>(ItemSelection<T> selection, IReadOnlyList<T> items, bool reveal)
        where T : class
    {
        var index = FindIndexIn(selection, items);
        if (index is null)
        {
            selection.Rows.Clear();
            return;
        }

        selection.Rows.Select(index.Value, items.Count, reveal);
    }

    /// <summary>Selects the item at the row the list widget left <see cref="ItemSelection{T}.Rows"/> at.</summary>
    public static void TakeFrom<T>(ItemSelection<T> selection, IReadOnlyList<T> items)
        where T : class
    {
        selection.Item = FindItemAt(items, selection.Rows.Index);
    }

    private static T? FindItemAt<T>(IReadOnlyList<T> items, int index)
        where T : class
    {
        if (index < 0)
        {
            return null;
        }

        if (index >= items.Count)
        {
            return null;
        }

        return items[index];
    }

    /// <summary>The selected item's row in <paramref name="items"/>; null when nothing is selected or the
    /// item is not one of them.</summary>
    public static int? FindIndexIn<T>(ItemSelection<T> selection, IReadOnlyList<T> items)
        where T : class
    {
        if (selection.Item is null)
        {
            return null;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], selection.Item))
            {
                return i;
            }
        }
        return null;
    }
}
