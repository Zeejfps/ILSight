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
    public string Filter = "";
    public bool SelectedOnly;

    public CloserRows Rows { get; } = new();

    /// <summary>Opens the dialog on <paramref name="assemblies"/>' items, none filtered out, with
    /// <paramref name="picked"/>, the assembly of the node selected in the tree, ticked when it is an item of
    /// the list; one read out of a package ticks nothing, so that its package is never closed unasked.</summary>
    public void Open(AssemblyList assemblies, LoadedAssembly? picked)
    {
        IsOpen = true;
        Filter = "";
        SelectedOnly = false;
        Rows.Start(assemblies, picked);
    }

    public void Close()
    {
        IsOpen = false;
        Rows.Clear();
    }

    /// <summary>Shows every item again.</summary>
    public void ClearFilter()
    {
        Filter = "";
        SelectedOnly = false;
    }
}
