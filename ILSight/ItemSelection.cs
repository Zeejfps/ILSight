using ZGF.Gui.Immediate.Widgets;

namespace ILSight;

/// <summary>The item selected in a list, held by itself, so rows added, removed or sorted around it
/// leave it selected. The list widget works by position: <see cref="ShowIn"/> gives it the item's
/// row before it records, and <see cref="TakeFrom"/> takes back the row it picked.</summary>
public sealed class ItemSelection<T> where T : class
{
    public T? Item { get; private set; }

    /// <summary>The selected item's row, for the list widget.</summary>
    public ListSelection Rows { get; } = new();

    /// <summary>Scrolls <paramref name="item"/> into view unless <paramref name="reveal"/> is false.</summary>
    public void Select(T item, IReadOnlyList<T> items, bool reveal = true)
    {
        Item = item;
        SelectRow(items, reveal);
    }

    public void Clear()
    {
        Item = null;
        Rows.Clear();
    }

    /// <summary>Selects the item <paramref name="by"/> rows on from the selected one, kept within
    /// <paramref name="items"/>; with nothing selected, a move back selects the first.</summary>
    public void Move(int by, IReadOnlyList<T> items)
    {
        ShowIn(items);
        Rows.Move(by, items.Count);
        TakeFrom(items);
    }

    /// <summary>Scrolls the selected item into view the next time the list is recorded.</summary>
    public void Reveal(IReadOnlyList<T> items)
    {
        if (Item is null)
        {
            return;
        }

        Select(Item, items);
    }

    public void DropIfGone(IReadOnlyList<T> items)
    {
        var index = FindIndexIn(items);
        if (index is not null)
        {
            return;
        }

        Clear();
    }

    /// <summary>Gives <see cref="Rows"/> the selected item's row in <paramref name="items"/>, keeping a reveal
    /// already asked for; call it before the list widget records.</summary>
    public void ShowIn(IReadOnlyList<T> items)
    {
        DropIfGone(items);
        SelectRow(items, reveal: false);
    }

    /// <summary>Selects the item at the row the list widget left <see cref="Rows"/> at.</summary>
    public void TakeFrom(IReadOnlyList<T> items)
    {
        Item = FindItemAt(items, Rows.Index);
    }

    public int? FindIndexIn(IReadOnlyList<T> items)
    {
        if (Item is null)
        {
            return null;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], Item))
            {
                return i;
            }
        }
        return null;
    }

    private void SelectRow(IReadOnlyList<T> items, bool reveal)
    {
        var index = FindIndexIn(items);
        if (index is null)
        {
            Rows.Clear();
            return;
        }

        Rows.Select(index.Value, items.Count, reveal);
    }

    private static T? FindItemAt(IReadOnlyList<T> items, int index)
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
}
