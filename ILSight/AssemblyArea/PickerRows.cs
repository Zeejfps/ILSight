using ILSight.ExportArea;

namespace ILSight.AssemblyArea;

/// <summary>The rows of the export dialog's Choose page: the open assemblies with the ticked ones first, in an order
/// fixed when the page opens so rows stay put while they are ticked, and the row last clicked, which a Shift-click's
/// range runs from.</summary>
public sealed class PickerRows
{
    private readonly List<ExportChoice> _order = [];
    private ExportChoice? _anchor;

    /// <summary>The assemblies in the order of the page.</summary>
    public IReadOnlyList<ExportChoice> Order => _order;

    /// <summary>Lists <paramref name="choices"/> afresh, ticked first, with no anchor.</summary>
    public void Arrange(IReadOnlyList<ExportChoice> choices)
    {
        _order.Clear();
        _order.AddRange(choices.Where(static c => c.Included));
        _order.AddRange(choices.Where(static c => !c.Included));
        _anchor = null;
    }

    /// <summary>Follows a click that set <paramref name="clicked"/>'s tick: with <paramref name="extend"/>
    /// (Shift held), every row in <paramref name="shown"/> from the anchor to it takes the same tick, as
    /// in a list of checkboxes elsewhere; rows outside the range keep theirs. Without an anchor among
    /// <paramref name="shown"/>, only <paramref name="clicked"/> changes. Either way it becomes the
    /// anchor. <paramref name="shown"/> is the list as shown before the click, so a row that
    /// Selected only hides once unticked still counts.</summary>
    public void TickClicked(IReadOnlyList<ExportChoice> shown, ExportChoice clicked, bool extend)
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

    private static int IndexOf(IReadOnlyList<ExportChoice> shown, ExportChoice choice)
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
