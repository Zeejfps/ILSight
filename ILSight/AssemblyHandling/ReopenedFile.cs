using ILSight.Core;

namespace ILSight.AssemblyHandling;

/// <summary>A file of the list read again: the item to list in place of the one before, and each tab's place
/// in it, null where it is gone, in the order the places were asked for.</summary>
public sealed record ReopenedFile
{
    public required LoadedAssembly Item { get; init; }

    public required BrowseNode?[] Places { get; init; }
}
