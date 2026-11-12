using ZGF.Gui.Immediate.Widgets;

namespace ILSight.AssemblyListArea;

/// <summary>What the assembly lists dialog shows: what it is doing, and the list picked in it.</summary>
public sealed class AssemblyListsDialog
{
    public ListsDialogMode Mode { get; set; } = new ListsDialogMode.Closed();

    public bool IsOpen => Mode is not ListsDialogMode.Closed;

    /// <summary>The picked row, an index into <see cref="SavedAssemblyLists.Names"/>.</summary>
    public ListSelection Selection { get; } = new();

    public bool IsAsking => Mode is ListsDialogMode.Renaming or ListsDialogMode.ConfirmingDelete;
}
