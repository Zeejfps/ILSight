using System.Runtime.CompilerServices;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.BuiltInArea;
using ILSight.UserArea;

namespace ILSight;

/// <summary>A class because ZGF's ITreeModel demands the methods here, which forward. The browse tree over the
/// open assemblies: its roots, the model the tree view reads and the view's expansion and selection. Children not built yet show a loading row and are asked to
/// be built, which the edge starts in the background, and children that failed to build a row saying why.</summary>
public sealed class BrowseTree : ITreeModel<BrowseNode>
{
    /// <summary>The browse tree whose nodes this shows.</summary>
    public required BrowseSession Session { get; init; }

    public readonly ConditionalWeakTable<BrowseNode, BrowseNode[]> ShownChildren = new();
    /// <summary>The nodes whose children were asked to be built while the tree drew, and those being built.</summary>
    public ChildBuilds Builds { get; } = new();

    public int RootsVersion = -1;
    public readonly UserIntent Expansion = new();
    public ApiVisibility Visibility = ApiVisibility.AllButCompilerGenerated;
    public bool ShowsEmptyTables;
    public bool NestsNamespaces;
    internal Dictionary<BrowseNode, RootLabel> RootLabels = new(ReferenceEqualityComparer.Instance);
    public UiTheme Theme = UiTheme.Dark;
    public NodeIconPalette IconColors = BuiltInThemes.Dark.Icons;
    public AppIcons Icons = BuiltInIconThemes.Reference.Icons;
    public TokenFormat Tokens = TokenFormat.Hidden;
    public IReadOnlyCollection<AssemblyPath> Reopening = [];

    public TreeViewState<BrowseNode> View { get; } = new(ReferenceEqualityComparer.Instance);

    public BrowseNode[] Roots = [];

    /// <summary>Changes whenever the roots change or namespaces start or stop nesting, which changes
    /// the paths and labels of the nodes.</summary>
    public int Revision;

    public bool HasChildren(BrowseNode node)
    {
        return Trees.HasChildren(this, node);
    }

    public TreeChildren<BrowseNode> GetChildren(BrowseNode node)
    {
        return Trees.GetChildren(this, node);
    }

    public TreeRow Row(BrowseNode node)
    {
        return Trees.GetRow(this, node);
    }

    public string KeyOf(BrowseNode node)
    {
        return Trees.GetKey(this, node);
    }
}
