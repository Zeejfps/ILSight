using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>A file of the list read again: the item to list in place of the one before, with what was read out of
/// it, and each tab's place in it, not found where it is gone, unreadable where in a module that could not be
/// read, in the order the places were asked for.</summary>
public sealed record ReopenedFile
{
    public required FreshItem Fresh { get; init; }

    public required Lookup<BrowseNode>[] Places { get; init; }
}
