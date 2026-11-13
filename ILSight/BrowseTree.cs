using System.Diagnostics;
using System.Runtime.CompilerServices;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.BuiltInArea;
using ILSight.UserArea;

namespace ILSight;

/// <summary>The browse tree over the open assemblies, as the tree view reads it (ZGF's ITreeModel): its roots, which
/// follow the open assemblies, what its rows show and how they expand, and the view's expansion and selection.
/// Children not built yet show a loading row and are asked to be built, which the edge starts in the background, and
/// children that failed to build a row saying why. Its roots and their labels, what it shows and in which style change
/// only through its methods, which keep the paths, labels and rows shown in step with them.</summary>
public sealed class BrowseTree : ITreeModel<BrowseNode>
{
    private const string LoadingLabel = "Loading…";

    /// <summary>The browse tree whose nodes this shows.</summary>
    public required BrowseSession Session { get; init; }

    private readonly ConditionalWeakTable<BrowseNode, BrowseNode[]> _shownChildren = new();
    /// <summary>The nodes whose children were asked to be built while the tree drew, and those being built.</summary>
    public ChildBuilds Builds { get; } = new();

    private int _rootsVersion = -1;
    public readonly UserIntent Expansion = new();
    private ApiVisibility _visibility = ApiVisibility.AllButCompilerGenerated;
    private bool _showsEmptyTables;
    private bool _nestsNamespaces;
    private Dictionary<BrowseNode, RootLabel> _rootLabels = new(ReferenceEqualityComparer.Instance);
    private UiTheme _theme = UiTheme.Dark;
    private NodeIconPalette _iconColors = BuiltInThemes.Dark.Icons;
    private AppIcons _icons = BuiltInIconThemes.Reference.Icons;
    private TokenFormat _tokens = TokenFormat.Hidden;
    private IReadOnlyCollection<AssemblyPath> _reopening = [];

    public TreeViewState<BrowseNode> View { get; } = new(ReferenceEqualityComparer.Instance);

    private BrowseNode[] _roots = [];

    private int _revision;

    public IReadOnlyList<BrowseNode> Roots => _roots;

    /// <summary>Changes whenever the roots change or namespaces start or stop nesting, which changes
    /// the paths and labels of the nodes.</summary>
    public int Revision => _revision;

    /// <summary>Rebuilds the roots when the assembly list changed. Returns true when it did.</summary>
    public bool TrySyncRoots(AssemblyList assemblies)
    {
        var version = assemblies.Version;
        if (version == _rootsVersion)
        {
            return false;
        }

        _rootsVersion = version;
        var listed = assemblies.Listed;
        var roots = new BrowseNode[listed.Count];
        for (var i = 0; i < roots.Length; i++)
        {
            roots[i] = Session.EnsureRoot(listed[i]);
        }

        FollowGoneRoots(roots);
        _roots = roots;
        _rootLabels = ListRootLabels(roots);
        _revision++;
        return true;
    }

    /// <summary>Moves what was expanded and selected under each root no longer among
    /// <paramref name="roots"/> to the root of its file read again, when one is, and forgets it.</summary>
    private void FollowGoneRoots(BrowseNode[] roots)
    {
        if (_roots.Length == 0)
        {
            return;
        }

        var kept = new HashSet<BrowseNode>(roots, ReferenceEqualityComparer.Instance);
        foreach (var root in _roots)
        {
            if (kept.Contains(root))
            {
                continue;
            }

            var replacement = FindRootOfFile(roots, root.Assembly.FilePath);
            FollowSelection(root, replacement);
            FollowExpansion(root, replacement);
        }
    }

    private static BrowseNode? FindRootOfFile(BrowseNode[] roots, AssemblyPath filePath)
    {
        foreach (var root in roots)
        {
            if (root.Assembly.FilePath.Equals(filePath))
            {
                return root;
            }
        }
        return null;
    }

    private void FollowSelection(BrowseNode gone, BrowseNode? replacement)
    {
        if (replacement is null)
        {
            return;
        }

        if (!View.TryGetSelected(out var selected))
        {
            return;
        }

        if (selected.Root != gone)
        {
            return;
        }

        var counterpart = FindCounterpart(selected, replacement);
        if (counterpart is not null)
        {
            View.Select(counterpart, scrollIntoView: false);
        }
    }

    /// <summary>The node standing where <paramref name="node"/> does, under <paramref name="root"/>
    /// in place of <paramref name="node"/>'s root, found through children already loaded.</summary>
    private BrowseNode? FindCounterpart(BrowseNode node, BrowseNode root)
    {
        if (node.Parent is null)
        {
            return root;
        }

        var parent = FindCounterpart(node.Parent, root);
        if (parent is null)
        {
            return null;
        }

        return Session.FindLoadedChildLike(parent, node);
    }

    /// <summary>Collapses <paramref name="node"/> and the nodes under it, expanding each one's
    /// counterpart under <paramref name="counterpart"/> instead.</summary>
    private void FollowExpansion(BrowseNode node, BrowseNode? counterpart)
    {
        if (!View.IsExpanded(node))
        {
            return;
        }

        View.SetExpanded(node, false);
        if (counterpart is not null)
        {
            View.SetExpanded(counterpart, true);
        }

        var children = Session.PeekChildren(node);
        if (children is not ChildrenState.Ready loaded)
        {
            return;
        }

        foreach (var child in loaded.Children)
        {
            FollowExpansion(child, FindChildLike(counterpart, child));
        }
    }

    private BrowseNode? FindChildLike(BrowseNode? parent, BrowseNode child)
    {
        if (parent is null)
        {
            return null;
        }

        return Session.FindLoadedChildLike(parent, child);
    }

    /// <summary>The node's name as tabs show it: assemblies listed more than once under one name
    /// also show their version, or their folder when that is the same too.</summary>
    public string GetLabel(BrowseNode node)
    {
        return node.Subject is NodeSubject.Module && _rootLabels.TryGetValue(node, out var label) ? label.Title : NodeText.Name(node);
    }

    /// <summary>The node's name as its row shows it, beside <see cref="NodeText.Secondary"/>: an
    /// assembly's folder is added only when the version and framework there do not tell it apart, and a
    /// namespace nested in another shows only its last segment.</summary>
    public string GetRowLabel(BrowseNode node)
    {
        if (IsNested(node))
        {
            return NodeText.NestedName(node);
        }

        return node.Subject is NodeSubject.Module && _rootLabels.TryGetValue(node, out var label) ? label.Row : NodeText.Name(node);
    }

    /// <summary>What tells a row apart from its siblings, for tests to find it by: its label, but an assembly by
    /// the name tabs give it, which adds its version, or its folder, only when another listed has its name.</summary>
    public string KeyOf(BrowseNode node)
    {
        if (node.Subject is NodeSubject.Module)
        {
            return GetLabel(node);
        }

        return GetRowLabel(node);
    }

    private bool IsNested(BrowseNode node)
    {
        return _nestsNamespaces && node.EnclosingNamespace is not null;
    }

    public void ShowApi(ApiVisibility visibility)
    {
        if (visibility == _visibility)
        {
            return;
        }

        _visibility = visibility;
        _shownChildren.Clear();
        View.Refresh();
    }

    /// <summary>Lists the metadata tables with no rows among the others, or leaves them out.</summary>
    public void ShowEmptyTables(bool show)
    {
        if (show == _showsEmptyTables)
        {
            return;
        }

        _showsEmptyTables = show;
        _shownChildren.Clear();
        View.Refresh();
    }

    /// <summary>Nests each namespace in the one its name extends by a segment, or lists them all
    /// under their assembly, keeping the selected node in view.</summary>
    public void NestNamespaces(bool nest)
    {
        if (nest == _nestsNamespaces)
        {
            return;
        }

        _nestsNamespaces = nest;
        _revision++;
        _shownChildren.Clear();
        View.Refresh();
        RevealSelected();
    }

    private void RevealSelected()
    {
        if (!View.TryGetSelected(out var selected))
        {
            return;
        }

        RevealNode(selected);
    }

    public void SetStyle(UiTheme theme, NodeIconPalette iconColors, AppIcons icons)
    {
        _theme = theme;
        _iconColors = iconColors;
        _icons = icons;
    }

    /// <summary>Writes each type's and member's metadata token after its name as <paramref name="format"/> says.</summary>
    public void ShowTokens(TokenFormat format)
    {
        _tokens = format;
    }

    /// <summary>Spins the roots of the assemblies read from <paramref name="files"/>, which are being reopened.</summary>
    public void ShowReopening(IReadOnlyCollection<AssemblyPath> files)
    {
        _reopening = files;
    }

    /// <summary>Expands the tree down to <paramref name="node"/>, selects it and scrolls to it. When
    /// it is hidden, or inside a hidden node, its nearest shown ancestor is selected instead.</summary>
    public void RevealNode(BrowseNode node)
    {
        var path = ListShownPathTo(node);
        View.Reveal(path);
    }

    /// <summary>The rows from the root down to <paramref name="node"/>, or to its nearest shown
    /// ancestor when it is hidden or inside a hidden node.</summary>
    private List<BrowseNode> ListShownPathTo(BrowseNode node)
    {
        var path = ListPathTo(node);
        var hidden = path.FindIndex(n => IsHidden(n));
        if (hidden > 0)
        {
            path.RemoveRange(hidden, path.Count - hidden);
        }

        return path;
    }

    /// <summary>The nodes from the root down to <paramref name="node"/>, each in the one whose row
    /// holds it, hidden ones included.</summary>
    public List<BrowseNode> ListPathTo(BrowseNode node)
    {
        var path = new List<BrowseNode>();
        for (var n = node; n is not null; n = FindShownParent(n))
        {
            path.Add(n);
        }

        path.Reverse();
        return path;
    }

    /// <summary>The node whose row holds <paramref name="node"/>'s: the namespace a namespace nests in
    /// when namespaces nest, else its parent.</summary>
    private BrowseNode? FindShownParent(BrowseNode node)
    {
        if (IsNested(node))
        {
            return node.EnclosingNamespace;
        }

        return node.Parent;
    }

    private bool IsHidden(BrowseNode node)
    {
        if (node.Subject is NodeSubject.MetadataTable table && table.Value.IsEmptyTable)
        {
            return !_showsEmptyTables;
        }

        if (node.Subject is NodeSubject.Namespace ns)
        {
            return IsNamespaceHidden(node, ns.Value);
        }

        return IsApiHidden(node);
    }

    /// <summary>Whether a namespace is left out: when it holds no type that shows and, when namespaces
    /// nest, no namespace that shows nests in it.</summary>
    private bool IsNamespaceHidden(BrowseNode node, NamespaceInfo ns)
    {
        var ownHidden = ns.IsEnclosingOnly || IsApiHidden(node);
        if (!ownHidden)
        {
            return false;
        }

        if (!_nestsNamespaces)
        {
            return true;
        }

        return AreInnerNamespacesHidden(node);
    }

    private bool AreInnerNamespacesHidden(BrowseNode node)
    {
        return Session.ListInnerNamespaces(node).All(inner => IsInnerNamespaceHidden(inner));
    }

    private bool IsInnerNamespaceHidden(BrowseNode node)
    {
        if (node.Subject is not NodeSubject.Namespace ns)
        {
            throw new UnreachableException();
        }

        return IsNamespaceHidden(node, ns.Value);
    }

    private bool IsApiHidden(BrowseNode node)
    {
        if (_visibility == ApiVisibility.All)
        {
            return false;
        }

        var visibility = node.Visibility;
        if (visibility.IsCompilerGenerated)
        {
            return true;
        }

        return _visibility == ApiVisibility.PublicOnly && !visibility.IsPublicApi;
    }

    /// <summary>The paths of the expanded nodes that are showing, for expanding them again after
    /// a reload.</summary>
    public List<NodePath> ListExpandedPaths()
    {
        var paths = new List<NodePath>();
        Collect(_roots);
        return paths;

        void Collect(IReadOnlyList<BrowseNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!View.IsExpanded(node))
                {
                    continue;
                }

                paths.Add(node.Path);
                var children = Session.PeekChildren(node);
                if (children is ChildrenState.Ready loaded)
                {
                    Collect(loaded.Children);
                }
            }
        }
    }

    /// <summary>Expands <paramref name="nodes"/>, e.g. those <see cref="ListExpandedPaths"/> named before
    /// a reload.</summary>
    public void ExpandNodes(IEnumerable<BrowseNode> nodes)
    {
        foreach (var node in nodes)
        {
            View.SetExpanded(node, true);
        }

        View.Refresh();
    }

    /// <summary>Expands every assembly, and begins expanding the namespaces in each, which
    /// <see cref="ExpandNamespacesHandler"/> finishes once the assembly's children are built. Collapsing before they
    /// are built keeps them collapsed.</summary>
    public NamespaceExpansion ExpandNamespaces()
    {
        var ticket = Expansion.Begin();
        var roots = _roots;
        foreach (var root in roots)
        {
            View.SetExpanded(root, true);
        }

        View.Refresh();
        return new NamespaceExpansion { Roots = roots, Ticket = ticket };
    }

    /// <summary>Expands the namespaces among <paramref name="children"/>, an assembly's.</summary>
    public void ExpandNamespacesIn(IReadOnlyList<BrowseNode> children)
    {
        foreach (var child in children)
        {
            ExpandIfNamespace(child);
        }

        View.Refresh();
    }

    private void ExpandIfNamespace(BrowseNode node)
    {
        if (node.Subject is NodeSubject.Namespace)
        {
            View.SetExpanded(node, true);
        }
    }

    public void CollapseAll()
    {
        Expansion.Begin();
        View.CollapseAll();
    }

    /// <summary>Collapses everything and selects nothing, as when the nodes it showed are gone.</summary>
    public void Forget()
    {
        View.ClearSelection();
        CollapseAll();
    }

    public bool HasChildren(BrowseNode node)
    {
        if (!_nestsNamespaces)
        {
            return Session.HasChildren(node);
        }

        return Session.HasChildren(node) || Session.ListInnerNamespaces(node).Count > 0;
    }

    /// <summary>The children shown under <paramref name="node"/>: those not hidden, with namespaces
    /// nested when the tree nests them; while they are being built a loading row, which asks for them to
    /// be built (<see cref="ChildBuilds"/>), and a row saying why when they could not be.</summary>
    public TreeChildren<BrowseNode> GetChildren(BrowseNode node)
    {
        var state = _nestsNamespaces ? Session.PeekNestedChildren(node) : Session.PeekChildren(node);
        return state.Match((tree: this, node),
            static (asked, _) => asked.tree.ShowLoading(asked.node),
            static (asked, _) => asked.tree.ShowLoading(asked.node),
            static (shown, ready) => TreeChildren<BrowseNode>.Of(shown.tree.EnsureShownChildren(shown.node, ready.Children)),
            static (shown, failed) => TreeChildren<BrowseNode>.Placeholder(shown.tree.CreateFailedRow(failed.Error)));
    }

    /// <summary>A loading row under <paramref name="node"/>, whose children are asked for.</summary>
    private TreeChildren<BrowseNode> ShowLoading(BrowseNode node)
    {
        Builds.Ask(node);
        return TreeChildren<BrowseNode>.Placeholder(CreateLoadingRow());
    }

    private BrowseNode[] EnsureShownChildren(BrowseNode node, IReadOnlyList<BrowseNode> children)
    {
        if (_shownChildren.TryGetValue(node, out var cached))
        {
            return cached;
        }

        return ListShownChildren(node, children);
    }

    /// <summary>Apart from <see cref="EnsureShownChildren"/>, which runs per expanded row every frame, as a lambda
    /// capturing a parameter costs its closure on the way in, before anything else is looked at.</summary>
    private BrowseNode[] ListShownChildren(BrowseNode node, IReadOnlyList<BrowseNode> children)
    {
        var shown = children
            .Where(c => !IsHidden(c))
            .ToArray();
        _shownChildren.AddOrUpdate(node, shown);
        return shown;
    }

    private TreeRow CreateLoadingRow()
    {
        return new TreeRow(LoadingLabel, NodeIcons.Loading(_iconColors, _icons), _theme.TextMuted, Spinning: true);
    }

    private TreeRow CreateFailedRow(Exception error)
    {
        return new TreeRow($"Error: {error.Message}", NodeIcons.Failed(_iconColors, _icons), Tooltip: error.Message);
    }

    public TreeRow Row(BrowseNode node)
    {
        var visibility = node.Visibility;
        return new TreeRow(
            GetRowLabel(node),
            NodeIcons.Icon(node, _iconColors, _icons),
            visibility.IsPublicApi ? 0u : _theme.TextMuted,
            Tooltip: NodeText.Tooltip(node),
            Secondary: FindSecondary(node),
            SecondaryBold: NodeText.PickedFramework(node),
            Spinning: IsReopening(node));
    }

    /// <summary>Whether the node is the root of a listed file being reopened.</summary>
    private bool IsReopening(BrowseNode node)
    {
        if (_reopening.Count == 0)
        {
            return false;
        }

        return node.Parent is null && _reopening.Contains(node.Assembly.FilePath);
    }

    /// <summary>The node's metadata token when the tree shows tokens and it has one, else what
    /// <see cref="NodeText.Secondary"/> adds.</summary>
    private string? FindSecondary(BrowseNode node)
    {
        var token = NodeText.Token(node, _tokens);
        if (token is not null)
        {
            return token;
        }

        return NodeText.Secondary(node);
    }

    private static Dictionary<BrowseNode, RootLabel> ListRootLabels(BrowseNode[] roots)
    {
        var labels = new Dictionary<BrowseNode, RootLabel>(ReferenceEqualityComparer.Instance);
        var byName = roots
            .Where(static r => r.Subject is NodeSubject.Module)
            .GroupBy(static r => r.Assembly.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var group in byName)
        {
            var nodes = group.ToList();
            if (nodes.Count == 1)
            {
                labels[nodes[0]] = new RootLabel { Title = nodes[0].Assembly.Name, Row = nodes[0].Assembly.Name };
                continue;
            }
            var versions = nodes.Select(static n => n.Assembly.Version?.ToString() ?? "").ToArray();
            var distinctVersions = versions.Distinct().Count();
            if (distinctVersions == nodes.Count)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    labels[nodes[i]] = new RootLabel
                    {
                        Title = nodes[i].Assembly.NameAndVersion,
                        Row = nodes[i].Assembly.Name,
                    };
                }

                continue;
            }
            var hints = ListFolderHints(nodes);
            for (var i = 0; i < nodes.Count; i++)
            {
                var label = $"{nodes[i].Assembly.Name} ({hints[i]})";
                labels[nodes[i]] = new RootLabel { Title = label, Row = label };
            }
        }
        return labels;
    }

    private static string[] ListFolderHints(List<BrowseNode> nodes)
    {
        var folders = nodes.Select(static n => (Path.GetDirectoryName(n.Assembly.FilePath.Text) ?? "").Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)).ToArray();
        var deepest = folders.Max(static f => f.Length);
        for (var depth = 1; depth <= deepest; depth++)
        {
            var hints = folders.Select(f => string.Join(Path.DirectorySeparatorChar, f[Math.Max(0, f.Length - depth)..])).ToArray();
            var distinctHints = hints.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (distinctHints == hints.Length)
            {
                return hints;
            }
        }
        return nodes.Select(static n => n.Assembly.FilePath.Text).ToArray();
    }

    /// <summary>How a root reads: as tabs show it, and as its row does.</summary>
    private readonly record struct RootLabel
    {
        public required string Title { get; init; }

        public required string Row { get; init; }
    }
}
