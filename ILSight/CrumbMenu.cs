using ILSight.Core;

namespace ILSight;

/// <summary>The children of one crumb of a document's omnibar, listed under it while it is open: those
/// whose name holds the filter typed, the crumb after it on the path selected once they are built. Opening,
/// it lists them unfiltered and its filter takes focus.</summary>
public sealed class CrumbMenu
{
    /// <summary>The children listed, as last filtered, and what they were filtered from and by.</summary>
    public readonly List<BrowseNode> Shown = new();
    public IReadOnlyList<BrowseNode>? Children;
    public string ShownFilter = "";

    /// <summary>The crumb to select once the children built hold it.</summary>
    public BrowseNode? ToSelect;
    public bool IsFocusPending = true;

    /// <summary>What a child's name must hold to be listed, ignoring case.</summary>
    public string Filter = "";

    /// <summary>The document panel whose bar the list is open in.</summary>
    public required string Panel { get; init; }

    /// <summary>The crumb whose children are listed.</summary>
    public required BrowseNode Node { get; init; }

    public ItemSelection<BrowseNode> Selection { get; } = new();
}
