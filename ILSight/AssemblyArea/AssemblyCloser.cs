using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>The "close assemblies" dialog: every item of the list in the list's order, ticked to close,
/// narrowed by a filter on name or path and, when asked, to the ticked ones. A Shift-click ticks or unticks
/// every shown row from the last one clicked, the anchor.</summary>
public sealed class AssemblyCloser
{
    /// <summary>Where the dialog's window is, kept between runs.</summary>
    public DialogWindow Window { get; } = new();

    public bool IsOpen;
    public bool FocusFilter;
    public string Filter = "";
    public bool SelectedOnly;

    /// <summary>The items of the list, in its order.</summary>
    public readonly List<CloseChoice> Order = new();

    /// <summary>The <see cref="AssemblyList.Version"/> <see cref="Order"/> was made from.</summary>
    public int ListedVersion = -1;

    /// <summary>The row last clicked, which a Shift-click's range runs from, or null.</summary>
    public CloseChoice? Anchor;
}
