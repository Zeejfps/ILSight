using ZGF.Gui.Immediate.Widgets;

namespace ILSight;

/// <summary>The item selected in a list, held by itself, so rows added, removed or sorted around it
/// leave it selected. The list widget works by position: <see cref="ItemSelections.ShowIn"/> gives it the item's
/// row before it records, and <see cref="ItemSelections.TakeFrom"/> takes back the row it picked.</summary>
public sealed class ItemSelection<T> where T : class
{
    public T? Item { get; set; }

    /// <summary>The selected item's row, for the list widget.</summary>
    public ListSelection Rows { get; } = new();
}

/// <summary>Moving what an <see cref="ItemSelection{T}"/> selects.</summary>
public static class ItemSelections
{
    /// <summary>Selects <paramref name="item"/> of <paramref name="items"/> and, unless told otherwise,
    /// scrolls it into view.</summary>
    public static void Select<T>(ItemSelection<T> selection, T item, IReadOnlyList<T> items, bool reveal = true)
        where T : class
    {
        selection.Item = item;
        selection.Rows.Select(FindIndexIn(selection, items), items.Count, reveal);
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
        if (selection.Item == null)
        {
            return;
        }

        Select(selection, selection.Item, items);
    }

    /// <summary>Drops the selection when the selected item is no longer one of <paramref name="items"/>.</summary>
    public static void DropIfGone<T>(ItemSelection<T> selection, IReadOnlyList<T> items)
        where T : class
    {
        if (FindIndexIn(selection, items) >= 0)
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
        selection.Rows.Select(FindIndexIn(selection, items), items.Count, reveal: false);
    }

    /// <summary>Selects the item at the row the list widget left <see cref="ItemSelection{T}.Rows"/> at.</summary>
    public static void TakeFrom<T>(ItemSelection<T> selection, IReadOnlyList<T> items)
        where T : class
    {
        selection.Item = GetItemAt(items, selection.Rows.Index);
    }

    private static T? GetItemAt<T>(IReadOnlyList<T> items, int index)
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

    /// <summary>The selected item's row in <paramref name="items"/>, or -1.</summary>
    public static int FindIndexIn<T>(ItemSelection<T> selection, IReadOnlyList<T> items)
        where T : class
    {
        if (selection.Item == null)
        {
            return -1;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], selection.Item))
            {
                return i;
            }
        }
        return -1;
    }
}
