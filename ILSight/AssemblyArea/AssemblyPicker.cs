namespace ILSight.AssemblyArea;

/// <summary>
/// The export dialog's Choose page: the open assemblies with the ticked ones first, in an order
/// fixed when the page opens so rows stay put while they are ticked, narrowed by a filter on name
/// or path and, when asked, to the ticked ones. A Shift-click ticks or unticks every shown row from
/// the last one clicked, the anchor.
/// </summary>
public sealed class AssemblyPicker
{
    public string Filter = "";
    public bool SelectedOnly;

    public PickerRows Rows { get; } = new();

    /// <summary>Shows every assembly again.</summary>
    public void ClearFilter()
    {
        Filter = "";
        SelectedOnly = false;
    }
}
