using ILSight.Core;

namespace ILSight;

/// <summary>The children of one crumb of a document's omnibar, listed under it while it is open: those
/// whose name holds the filter typed, the crumb after it on the path selected once they are built.</summary>
public sealed class CrumbMenu
{
    /// <summary>The children listed, as last filtered, and what they were filtered from and by.</summary>
    public readonly List<BrowseNode> Shown = new();
    public IReadOnlyList<BrowseNode>? Children;
    public string ShownFilter = "";

    /// <summary>The crumb to select once the children built hold it.</summary>
    public BrowseNode? ToSelect;
    public bool IsFocusPending = true;

    /// <summary>Lists the children of <paramref name="node"/>, in the bar of <paramref name="panel"/>,
    /// unfiltered, with <paramref name="next"/>, the crumb after it, to be selected; its filter takes focus.</summary>
    public CrumbMenu(string panel, BrowseNode node, BrowseNode? next)
    {
        Panel = panel;
        Node = node;
        ToSelect = next;
    }

    /// <summary>What a child's name must hold to be listed, ignoring case.</summary>
    public string Filter = "";

    /// <summary>The document panel whose bar the list is open in.</summary>
    public string Panel { get; }

    /// <summary>The crumb whose children are listed.</summary>
    public BrowseNode Node { get; }

    public ItemSelection<BrowseNode> Selection { get; } = new();
}
