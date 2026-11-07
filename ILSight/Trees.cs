using ZGF.Gui.Immediate.Widgets;
using ZGF.Gui.Immediate;
using System.Diagnostics;
using ILSight.Core;

namespace ILSight;

/// <summary>The browse tree following the open assemblies, as does the analyzer's rows following the analyses,
/// and what the tree's rows show and how they expand.</summary>
public static class Trees
{
    private const string LoadingLabel = "Loading…";

    /// <summary>Forgets what the tree had expanded and selected, and the menu's target, once the list
    /// was cleared; rebuilds its roots when the open assemblies changed; and lists the analyzer's rows
    /// again when its roots changed. The roots of the assemblies read from <paramref name="reopening"/>
    /// spin while they are reopened.</summary>
    public static void SyncTrees(UiState uiState, AssemblyList assemblies, AnalyzerTree analyzer, ApiVisibility visibility,
        IReadOnlyCollection<AssemblyPath> reopening)
    {
        ForgetClearedList(uiState, assemblies);
        Trees.ShowApi(uiState.Tree, visibility);
        Trees.SyncRoots(uiState.Tree, assemblies);
        Trees.ShowReopening(uiState.Tree, reopening);
        Analysis.FollowTree(uiState.Analyzer, analyzer);
    }

    private static void ForgetClearedList(UiState uiState, AssemblyList assemblies)
    {
        if (!ShownChanges.TakeListGeneration(uiState, assemblies.Generation))
            return;
        Trees.ForgetTree(uiState.Tree);
        uiState.MenuTarget = null;
    }

    /// <summary>Rebuilds the roots when the assembly list changed. Returns true when it did.</summary>
    public static bool SyncRoots(BrowseTree tree, AssemblyList assemblies)
    {
        var version = assemblies.Version;
        if (version == tree.RootsVersion) return false;
        tree.RootsVersion = version;
        var listed = assemblies.Listed;
        var roots = new BrowseNode[listed.Count];
        for (var i = 0; i < roots.Length; i++)
            roots[i] = BrowseSessions.EnsureRoot(tree.Session, listed[i]);
        FollowGoneRoots(tree, roots);
        tree.Roots = roots;
        tree.RootLabels = ListRootLabels(roots);
        tree.Revision++;
        return true;
    }

    /// <summary>Moves what was expanded and selected under each root no longer among
    /// <paramref name="roots"/> to the root of its file read again, when one is, and forgets it.</summary>
    private static void FollowGoneRoots(BrowseTree tree, BrowseNode[] roots)
    {
        if (tree.Roots.Length == 0)
            return;
        var kept = new HashSet<BrowseNode>(roots, ReferenceEqualityComparer.Instance);
        foreach (var root in tree.Roots)
        {
            if (kept.Contains(root))
                continue;
            var replacement = FindRootOfFile(roots, root.Assembly.FilePath);
            FollowSelection(tree, root, replacement);
            FollowExpansion(tree, root, replacement);
        }
    }

    private static BrowseNode? FindRootOfFile(BrowseNode[] roots, AssemblyPath filePath)
    {
        foreach (var root in roots)
        {
            if (root.Assembly.FilePath.Equals(filePath))
                return root;
        }
        return null;
    }

    private static void FollowSelection(BrowseTree tree, BrowseNode gone, BrowseNode? replacement)
    {
        if (replacement == null)
            return;
        if (!tree.View.TryGetSelected(out var selected))
            return;
        if (selected.Root != gone)
            return;
        var counterpart = FindCounterpart(tree, selected, replacement);
        if (counterpart != null)
            tree.View.Select(counterpart, scrollIntoView: false);
    }

    /// <summary>The node standing where <paramref name="node"/> does, under <paramref name="root"/>
    /// in place of <paramref name="node"/>'s root, found through children already loaded.</summary>
    private static BrowseNode? FindCounterpart(BrowseTree tree, BrowseNode node, BrowseNode root)
    {
        if (node.Parent == null)
            return root;
        var parent = FindCounterpart(tree, node.Parent, root);
        if (parent == null)
            return null;
        return BrowseSessions.FindLoadedChildLike(tree.Session, parent, node);
    }

    /// <summary>Collapses <paramref name="node"/> and the nodes under it, expanding each one's
    /// counterpart under <paramref name="counterpart"/> instead.</summary>
    private static void FollowExpansion(BrowseTree tree, BrowseNode node, BrowseNode? counterpart)
    {
        if (!tree.View.IsExpanded(node))
            return;
        tree.View.SetExpanded(node, false);
        if (counterpart != null)
            tree.View.SetExpanded(counterpart, true);
        if (BrowseSessions.PeekChildren(tree.Session, node) is not ChildrenState.Ready loaded)
            return;
        foreach (var child in loaded.Children)
            FollowExpansion(tree, child, FindChildLike(tree, counterpart, child));
    }

    private static BrowseNode? FindChildLike(BrowseTree tree, BrowseNode? parent, BrowseNode child)
    {
        if (parent == null)
            return null;
        return BrowseSessions.FindLoadedChildLike(tree.Session, parent, child);
    }

    /// <summary>The node's name as tabs show it: assemblies listed more than once under one name
    /// also show their version, or their folder when that is the same too.</summary>
    public static string GetLabel(BrowseTree tree, BrowseNode node)
    {
        return node.Subject is NodeSubject.Module && tree.RootLabels.TryGetValue(node, out var label) ? label.Title : NodeText.Name(node);
    }

    /// <summary>The node's name as its row shows it, beside <see cref="NodeText.Secondary"/>: an
    /// assembly's folder is added only when the version and framework there do not tell it apart, and a
    /// namespace nested in another shows only its last segment.</summary>
    public static string GetRowLabel(BrowseTree tree, BrowseNode node)
    {
        if (IsNested(tree, node))
            return NodeText.NestedName(node);
        return node.Subject is NodeSubject.Module && tree.RootLabels.TryGetValue(node, out var label) ? label.Row : NodeText.Name(node);
    }

    private static bool IsNested(BrowseTree tree, BrowseNode node)
    {
        return tree.NestsNamespaces && node.EnclosingNamespace != null;
    }

    public static void ShowApi(BrowseTree tree, ApiVisibility visibility)
    {
        if (visibility == tree.Visibility) return;
        tree.Visibility = visibility;
        tree.ShownChildren.Clear();
        tree.View.Refresh();
    }

    /// <summary>Lists the metadata tables with no rows among the others, or leaves them out.</summary>
    public static void ShowEmptyTables(BrowseTree tree, bool show)
    {
        if (show == tree.ShowsEmptyTables) return;
        tree.ShowsEmptyTables = show;
        tree.ShownChildren.Clear();
        tree.View.Refresh();
    }

    /// <summary>Nests each namespace in the one its name extends by a segment, or lists them all
    /// under their assembly, keeping the selected node in view.</summary>
    public static void NestNamespaces(BrowseTree tree, bool nest)
    {
        if (nest == tree.NestsNamespaces) return;
        tree.NestsNamespaces = nest;
        tree.Revision++;
        tree.ShownChildren.Clear();
        tree.View.Refresh();
        RevealSelected(tree);
    }

    private static void RevealSelected(BrowseTree tree)
    {
        if (!tree.View.TryGetSelected(out var selected))
            return;
        RevealNode(tree, selected);
    }

    public static void SetTreeStyle(BrowseTree tree, UiTheme theme, NodeIconPalette iconColors, AppIcons icons)
    {
        tree.Theme = theme;
        tree.IconColors = iconColors;
        tree.Icons = icons;
    }

    /// <summary>Writes each type's and member's metadata token after its name as <paramref name="format"/> says.</summary>
    public static void ShowTokens(BrowseTree tree, TokenFormat format)
    {
        tree.Tokens = format;
    }

    /// <summary>Spins the roots of the assemblies read from <paramref name="files"/>, which are being reopened.</summary>
    public static void ShowReopening(BrowseTree tree, IReadOnlyCollection<AssemblyPath> files)
    {
        tree.Reopening = files;
    }

    /// <summary>Expands the tree down to <paramref name="node"/>, selects it and scrolls to it. When
    /// it is hidden, or inside a hidden node, its nearest shown ancestor is selected instead.</summary>
    public static void RevealNode(BrowseTree tree, BrowseNode node)
    {
        var path = ListShownPathTo(tree, node);
        tree.View.Reveal(path);
    }

    /// <summary>The rows from the root down to <paramref name="node"/>, or to its nearest shown
    /// ancestor when it is hidden or inside a hidden node.</summary>
    private static List<BrowseNode> ListShownPathTo(BrowseTree tree, BrowseNode node)
    {
        var path = ListPathTo(tree, node);
        var hidden = path.FindIndex(n => IsHidden(tree, n));
        if (hidden > 0)
            path.RemoveRange(hidden, path.Count - hidden);
        return path;
    }

    /// <summary>The nodes from the root down to <paramref name="node"/>, each in the one whose row
    /// holds it, hidden ones included.</summary>
    public static List<BrowseNode> ListPathTo(BrowseTree tree, BrowseNode node)
    {
        var path = new List<BrowseNode>();
        for (var n = node; n != null; n = FindShownParent(tree, n))
            path.Add(n);
        path.Reverse();
        return path;
    }

    /// <summary>The node whose row holds <paramref name="node"/>'s: the namespace a namespace nests in
    /// when namespaces nest, else its parent.</summary>
    private static BrowseNode? FindShownParent(BrowseTree tree, BrowseNode node)
    {
        if (IsNested(tree, node))
            return node.EnclosingNamespace;
        return node.Parent;
    }

    private static bool IsHidden(BrowseTree tree, BrowseNode node)
    {
        if (node.Subject is NodeSubject.MetadataTable table && table.Value.IsEmptyTable) return !tree.ShowsEmptyTables;
        if (node.Subject is NodeSubject.Namespace ns) return IsNamespaceHidden(tree, node, ns.Value);
        return IsApiHidden(tree, node);
    }

    /// <summary>Whether a namespace is left out: when it holds no type that shows and, when namespaces
    /// nest, no namespace that shows nests in it.</summary>
    private static bool IsNamespaceHidden(BrowseTree tree, BrowseNode node, NamespaceInfo ns)
    {
        var ownHidden = ns.IsEnclosingOnly || IsApiHidden(tree, node);
        if (!ownHidden) return false;
        if (!tree.NestsNamespaces) return true;
        return AreInnerNamespacesHidden(tree, node);
    }

    private static bool AreInnerNamespacesHidden(BrowseTree tree, BrowseNode node)
    {
        return BrowseSessions.ListInnerNamespaces(tree.Session, node).All(inner => IsInnerNamespaceHidden(tree, inner));
    }

    private static bool IsInnerNamespaceHidden(BrowseTree tree, BrowseNode node)
    {
        if (node.Subject is not NodeSubject.Namespace ns)
            throw new UnreachableException();
        return IsNamespaceHidden(tree, node, ns.Value);
    }

    private static bool IsApiHidden(BrowseTree tree, BrowseNode node)
    {
        if (tree.Visibility == ApiVisibility.All) return false;
        var visibility = NodeDefinitions.GetVisibility(node.Subject);
        if (visibility.IsCompilerGenerated) return true;
        return tree.Visibility == ApiVisibility.PublicOnly && !visibility.IsPublicApi;
    }

    /// <summary>The paths of the expanded nodes that are showing, for expanding them again after
    /// a reload.</summary>
    public static List<NodePath> ListExpandedPaths(BrowseTree tree)
    {
        var paths = new List<NodePath>();
        Collect(tree.Roots);
        return paths;

        void Collect(IReadOnlyList<BrowseNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (!tree.View.IsExpanded(node)) continue;
                paths.Add(BrowseNodes.GetPath(node));
                if (BrowseSessions.PeekChildren(tree.Session, node) is ChildrenState.Ready loaded)
                    Collect(loaded.Children);
            }
        }
    }

    /// <summary>Expands <paramref name="nodes"/>, e.g. those <see cref="ListExpandedPaths"/> named before
    /// a reload.</summary>
    public static void ExpandNodes(BrowseTree tree, IEnumerable<BrowseNode> nodes)
    {
        foreach (var node in nodes)
            tree.View.SetExpanded(node, true);
        tree.View.Refresh();
    }

    /// <summary>Expands every assembly and the namespaces in it, building an assembly's children in the
    /// background first when they are not built yet. Collapsing before they are built keeps it collapsed.</summary>
    public static void ExpandNamespaces(BrowseTree tree)
    {
        var ticket = tree.Expansion.Begin();
        foreach (var root in tree.Roots)
            ExpandAssembly(tree, root, ticket);
        tree.View.Refresh();
    }

    private static void ExpandAssembly(BrowseTree tree, BrowseNode root, int ticket)
    {
        Background.Start(ExpandAssemblyAsync(tree, root, ticket), tree.ReportError);
    }

    private static async Task ExpandAssemblyAsync(BrowseTree tree, BrowseNode root, int ticket)
    {
        tree.View.SetExpanded(root, true);
        var built = await Background.Wait(BrowseSessions.LoadChildrenAsync(tree.Session, root));
        if (tree.Expansion.IsStale(ticket))
            return;
        if (built is not Outcome<ChildrenState>.Ok ok)
            return;
        if (ok.Value is not ChildrenState.Ready loaded)
            return;
        foreach (var child in loaded.Children)
            ExpandIfNamespace(tree, child);
        tree.View.Refresh();
    }

    private static void ExpandIfNamespace(BrowseTree tree, BrowseNode node)
    {
        if (node.Subject is NodeSubject.Namespace)
            tree.View.SetExpanded(node, true);
    }

    public static void CollapseTree(BrowseTree tree)
    {
        tree.Expansion.Begin();
        tree.View.CollapseAll();
    }

    /// <summary>Collapses everything and selects nothing, as when the nodes it showed are gone.</summary>
    public static void ForgetTree(BrowseTree tree)
    {
        tree.View.ClearSelection();
        CollapseTree(tree);
    }

    public static bool HasChildren(BrowseTree tree, BrowseNode node)
    {
        if (!tree.NestsNamespaces) return BrowseSessions.HasChildren(tree.Session, node);
        return BrowseSessions.HasChildren(tree.Session, node) || BrowseSessions.ListInnerNamespaces(tree.Session, node).Count > 0;
    }

    /// <summary>The children shown under <paramref name="node"/>: those not hidden, with namespaces
    /// nested when the tree nests them; while they are being built a loading row, which starts
    /// building them, and a row saying why when they could not be.</summary>
    public static TreeChildren<BrowseNode> GetChildren(BrowseTree tree, BrowseNode node)
    {
        var state = tree.NestsNamespaces ? BrowseSessions.PeekNestedChildren(tree.Session, node) : BrowseSessions.PeekChildren(tree.Session, node);
        switch (state)
        {
            case ChildrenState.Ready ready:
                return TreeChildren<BrowseNode>.Of(EnsureShownChildren(tree, node, ready.Children));
            case ChildrenState.Failed failed:
                return TreeChildren<BrowseNode>.Placeholder(CreateFailedRow(tree, failed.Error));
            case ChildrenState.Running or ChildrenState.Unloaded:
                BuildChildren(tree, node);
                return TreeChildren<BrowseNode>.Placeholder(CreateLoadingRow(tree));
            default:
                throw new UnreachableException();
        }
    }

    private static BrowseNode[] EnsureShownChildren(BrowseTree tree, BrowseNode node, IReadOnlyList<BrowseNode> children)
    {
        if (tree.ShownChildren.TryGetValue(node, out var cached)) return cached;
        return ListShownChildren(tree, node, children);
    }

    /// <summary>Apart from <see cref="EnsureShownChildren"/>, which runs per expanded row every frame, as a lambda
    /// capturing a parameter costs its closure on the way in, before anything else is looked at.</summary>
    private static BrowseNode[] ListShownChildren(BrowseTree tree, BrowseNode node, IReadOnlyList<BrowseNode> children)
    {
        var shown = children
            .Where(c => !IsHidden(tree, c))
            .ToArray();
        tree.ShownChildren.AddOrUpdate(node, shown);
        return shown;
    }

    private static TreeRow CreateLoadingRow(BrowseTree tree)
    {
        return new(LoadingLabel, NodeIcons.Loading(tree.IconColors, tree.Icons), tree.Theme.TextMuted, Spinning: true);
    }

    private static TreeRow CreateFailedRow(BrowseTree tree, Exception error)
    {
        return new($"Error: {error.Message}", NodeIcons.Failed(tree.IconColors, tree.Icons), Tooltip: error.Message);
    }

    public static TreeRow GetRow(BrowseTree tree, BrowseNode node)
    {
        return new(
            GetRowLabel(tree, node),
            NodeIcons.Icon(node, tree.IconColors, tree.Icons),
            NodeDefinitions.GetVisibility(node.Subject).IsPublicApi ? 0u : tree.Theme.TextMuted,
            Tooltip: NodeText.Tooltip(node),
            Secondary: GetSecondary(tree, node),
            SecondaryBold: NodeText.PickedFramework(node),
            Spinning: IsReopening(tree, node));
    }

    /// <summary>Whether the node is the root of a listed file being reopened.</summary>
    private static bool IsReopening(BrowseTree tree, BrowseNode node)
    {
        if (tree.Reopening.Count == 0)
            return false;
        return node.Parent == null && tree.Reopening.Contains(node.Assembly.FilePath);
    }

    /// <summary>The node's metadata token when the tree shows tokens and it has one, else what
    /// <see cref="NodeText.Secondary"/> adds.</summary>
    private static string? GetSecondary(BrowseTree tree, BrowseNode node)
    {
        var token = NodeText.Token(node, tree.Tokens);
        if (token != null)
            return token;
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
                labels[nodes[0]] = new RootLabel(nodes[0].Assembly.Name, nodes[0].Assembly.Name);
                continue;
            }
            var versions = nodes.Select(static n => n.Assembly.Version?.ToString() ?? "").ToArray();
            if (versions.Distinct().Count() == nodes.Count)
            {
                for (var i = 0; i < nodes.Count; i++)
                    labels[nodes[i]] = new RootLabel(nodes[i].Assembly.NameAndVersion, nodes[i].Assembly.Name);
                continue;
            }
            var hints = ListFolderHints(nodes);
            for (var i = 0; i < nodes.Count; i++)
            {
                var label = $"{nodes[i].Assembly.Name} ({hints[i]})";
                labels[nodes[i]] = new RootLabel(label, label);
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
            if (hints.Distinct(StringComparer.OrdinalIgnoreCase).Count() == hints.Length)
                return hints;
        }
        return nodes.Select(static n => n.Assembly.FilePath.Text).ToArray();
    }

    private static void BuildChildren(BrowseTree tree, BrowseNode node)
    {
        Background.Start(BuildChildrenAsync(tree, node), tree.ReportError);
    }

    private static async Task BuildChildrenAsync(BrowseTree tree, BrowseNode node)
    {
        if (!tree.Building.Add(node)) return;
        await Background.Wait(BrowseSessions.LoadChildrenAsync(tree.Session, node));
        tree.Building.Remove(node);
        tree.View.Refresh();
    }
}
