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
