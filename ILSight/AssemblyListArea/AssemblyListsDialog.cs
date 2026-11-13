using ZGF.Gui.Immediate.Widgets;

namespace ILSight.AssemblyListArea;

/// <summary>What the assembly lists dialog shows: what it is doing, and the list picked in it.</summary>
public sealed class AssemblyListsDialog
{
    public ListsDialogMode Mode { get; private set; } = new ListsDialogMode.Closed();

    public bool IsOpen => Mode is not ListsDialogMode.Closed;

    /// <summary>The picked row, an index into <see cref="SavedAssemblyLists.Names"/>.</summary>
    public ListSelection Selection { get; } = new();

    public bool IsAsking => Mode is ListsDialogMode.Renaming or ListsDialogMode.ConfirmingDelete;

    /// <summary>Opens the dialog with the open list of <paramref name="lists"/> picked.</summary>
    public void Open(SavedAssemblyLists lists)
    {
        Mode = new ListsDialogMode.Browsing();
        PickList(lists, lists.Active);
    }

    /// <summary>Picks the list named <paramref name="name"/> and starts renaming it.</summary>
    public void PickToRename(SavedAssemblyLists lists, string name)
    {
        PickList(lists, name);
        StartRenaming(name);
    }

    /// <summary>Picks the list named <paramref name="name"/>, leaving any rename or deletion.</summary>
    public void PickList(SavedAssemblyLists lists, string name)
    {
        var names = lists.Names;
        var index = FindListIndex(names, name);
        if (index is null)
        {
            Selection.Clear();
        }
        else
        {
            Selection.Select(index.Value, names.Count);
        }

        StopAsking();
    }

    /// <summary>The name of the picked list, or null.</summary>
    public string? FindPickedList(SavedAssemblyLists lists)
    {
        var names = lists.Names;
        var index = Selection.Index;
        if (index < 0)
        {
            return null;
        }

        if (index >= names.Count)
        {
            return null;
        }

        return names[index];
    }

    public void StartRenaming(string name)
    {
        Mode = new ListsDialogMode.Renaming(name);
    }

    public void StartConfirmingDelete(string name)
    {
        Mode = new ListsDialogMode.ConfirmingDelete(name);
    }

    /// <summary>Leaves the rename or deletion being asked about.</summary>
    public void StopAsking()
    {
        if (!IsAsking)
        {
            return;
        }

        Mode = new ListsDialogMode.Browsing();
    }

    public void Close()
    {
        Mode = new ListsDialogMode.Closed();
    }

    private static int? FindListIndex(IReadOnlyList<string> names, string name)
    {
        var trimmed = name.Trim();
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return null;
    }
}
