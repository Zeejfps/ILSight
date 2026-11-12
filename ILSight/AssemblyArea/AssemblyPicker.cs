using ILSight.Exporting;

namespace ILSight.AssemblyArea;

/// <summary>
/// The export dialog's Choose page: the open assemblies with the ticked ones first, in an order
/// fixed when the page opens so rows stay put while they are ticked, narrowed by a filter on name
/// or path and, when asked, to the ticked ones. A Shift-click ticks or unticks every shown row from
/// the last one clicked, the anchor.
/// </summary>
public sealed class AssemblyPicker
{
    /// <summary>The assemblies in the order of the page.</summary>
    public readonly List<ExportChoice> Order = new();

    public string Filter = "";
    public bool SelectedOnly;

    /// <summary>The row last clicked, which a Shift-click's range runs from, or null.</summary>
    public ExportChoice? Anchor;
}
