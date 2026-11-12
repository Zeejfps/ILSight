using System.Reflection.Metadata.Ecma335;
using ILSight.Core.Metadata;
using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILSight.Core;

/// <summary>
/// The browse tree of the assemblies it is asked about: the root of each, the children of each node,
/// built once and handed out whole, and the node of each type and member by its handle. A node keeps
/// its identity for as long as its assembly lives, and what the session holds of an assembly goes when
/// the assembly does. All of <see cref="BrowseSessions"/>, which is the way to it, is thread-safe.
/// </summary>
public sealed class BrowseSession
{
    public readonly ConditionalWeakTable<LoadedModule, BrowseNode> ModuleRoots = new();
    public readonly ConditionalWeakTable<LoadedModule, ConcurrentDictionary<string, BrowseNode>> Namespaces = new();
    public readonly ConditionalWeakTable<BrowseNode, IReadOnlyList<BrowseNode>> Nested = new();
    public readonly ConditionalWeakTable<BrowseNode, IReadOnlyList<BrowseNode>> Inner = new();
    public readonly ConditionalWeakTable<LoadedAssembly, BrowseNode> OtherRoots = new();
    public readonly ConditionalWeakTable<LoadedModule, ConcurrentDictionary<EntityHandle, BrowseNode>> Index = new();

    /// <summary>Where each node's children stand, which can only be read or changed holding its lock.</summary>
    internal readonly ConditionalWeakTable<BrowseNode, Guarded<ChildrenState>> Children = new();

    /// <summary>The root of the listed <paramref name="item"/>'s tree (a module not read out of a package, a package,
    /// a file that could not be read), made from the item's values the first time, then kept. Reads no file. A module
    /// read out of a package is no listed item: its root sits in its package's tree
    /// (<see cref="BrowseSessions.EnsureRoot"/>).</summary>
    public BrowseNode EnsureRoot(LoadedAssembly item)
    {
        if (item is LoadedModule module)
        {
            if (module.Place is not null)
            {
                throw new ArgumentException($"{module.Name} is read out of a package, which is the listed item.", nameof(item));
            }

            return ModuleRoots.GetValue(module, static m => BrowseRoots.Create(m));
        }

        return OtherRoots.GetValue(item, static a => BrowseRoots.Create(a));
    }

    /// <summary>The table node and row that <paramref name="address"/>, a link of the grid <paramref name="grid"/> shows,
    /// leads to, among the tables of the metadata the grid lists; null when <paramref name="grid"/> lists no metadata, or
    /// that metadata lists no such table. Reads this session's nodes of the grid's metadata folder: for a table's grid
    /// they are built (the grid's node is one of the folder's tables); for another grid of the folder (a header's), the
    /// tables are made now from the image in memory, a node per table a file can have, and kept as the folder's tables.
    /// Never waits: null while another thread builds them, and for an image that isn't in memory.</summary>
    public MetadataTarget? FindMetadataRow(BrowseNode grid, MetadataAddress address)
    {
        var folder = FindMetadataFolder(grid);
        if (folder is null)
        {
            return null;
        }

        var tables = FindBuiltChild(folder.Value.Node, static c => c.Subject is NodeSubject.MetadataTables);
        if (tables is null)
        {
            return null;
        }

        var listed = EnsureTables(tables, folder.Value.Info);
        var table = FindTable(listed, address.Table);
        if (table is null)
        {
            return null;
        }

        return new MetadataTarget { Node = table, Row = address.Row };
    }

    /// <summary>The "Metadata" folder <paramref name="node"/> belongs to, and what it lists.</summary>
    private static (BrowseNode Node, MetadataFolderInfo Info)? FindMetadataFolder(BrowseNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent.Subject is NodeSubject.Metadata metadata)
            {
                return (parent, metadata.Value);
            }
        }
        return null;
    }

    /// <summary>The first of <paramref name="node"/>'s built children that <paramref name="match"/> picks; null when its
    /// children aren't built or none is picked.</summary>
    private BrowseNode? FindBuiltChild(BrowseNode node, Func<BrowseNode, bool> match)
    {
        var children = PeekChildren(node);
        if (children is not ChildrenState.Ready ready)
        {
            return null;
        }

        return ready.Children.FirstOrDefault(match);
    }

    /// <summary>The node listing the rows of <paramref name="table"/> among <paramref name="tables"/>; null when none
    /// does.</summary>
    private static BrowseNode? FindTable(IReadOnlyList<BrowseNode> tables, TableIndex table)
    {
        foreach (var node in tables)
        {
            if (node.Subject is NodeSubject.MetadataTable part && part.Value.ListedTable == table)
            {
                return node;
            }
        }
        return null;
    }

    /// <summary>The children of <paramref name="tables"/>, a metadata folder's "Tables" node: those built, else made now
    /// from <paramref name="folder"/>'s image in memory and kept, as the tree's build keeps them (a read that fails is
    /// kept as their failure, which the tree shows). None while another thread builds them, after a failed build, and
    /// for an image that isn't in memory.</summary>
    private IReadOnlyList<BrowseNode> EnsureTables(BrowseNode tables, MetadataFolderInfo folder)
    {
        var slot = Children.GetValue(tables, static _ => new Guarded<ChildrenState>(ChildrenState.NotLoaded));
        TaskCompletionSource<ChildrenState> building;
        using (var held = slot.Enter())
        {
            if (held.Value is ChildrenState.Ready ready)
            {
                return ready.Children;
            }

            if (held.Value != ChildrenState.NotLoaded)
            {
                return [];
            }

            if (!folder.Module.IsImageInMemory)
            {
                return [];
            }

            building = new TaskCompletionSource<ChildrenState>(TaskCreationOptions.RunContinuationsAsynchronously);
            held.Value = new ChildrenState.Running(building.Task);
        }

        var state = BuildTables(tables, folder);
        using (var held = slot.Enter())
        {
            held.Value = state;
        }

        building.SetResult(state);
        return state is ChildrenState.Ready built ? built.Children : [];
    }

    private static ChildrenState BuildTables(BrowseNode tables, MetadataFolderInfo folder)
    {
        try
        {
            return new ChildrenState.Ready(MetadataTableNodes.List(tables, folder));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new ChildrenState.Failed(e);
        }
    }

    /// <summary>Where <paramref name="node"/>'s children stand now; never blocks or builds them.</summary>
    public ChildrenState PeekChildren(BrowseNode node)
    {
        if (!Children.TryGetValue(node, out var slot))
        {
            return ChildrenState.NotLoaded;
        }

        using var held = slot.Enter();
        return held.Value;
    }

    /// <summary><see cref="PeekChildren"/>, with the children as the tree lists them when it nests
    /// namespaces: for an assembly, its children but the namespaces nested in others; for a namespace,
    /// the namespaces nested in it and its own children, sorted together by name.</summary>
    public ChildrenState PeekNestedChildren(BrowseNode node)
    {
        var state = PeekChildren(node);
        if (state is not ChildrenState.Ready ready)
        {
            return state;
        }

        if (Nested.TryGetValue(node, out var nested))
        {
            return new ChildrenState.Ready(nested);
        }

        return new ChildrenState.Ready(AddNested(node, ready.Children));
    }

    /// <summary>Apart from <see cref="PeekNestedChildren"/>, which the tree asks per expanded row every frame,
    /// so that only the first time pays for the closure.</summary>
    private IReadOnlyList<BrowseNode> AddNested(BrowseNode node, IReadOnlyList<BrowseNode> children)
    {
        return Nested.GetValue(node, n => NestChildren(n, children));
    }

    /// <summary>Whether <paramref name="node"/> has children, answered without building them where
    /// possible. Children that failed to build count, so that why can be shown.</summary>
    public bool HasChildren(BrowseNode node)
    {
        return PeekChildren(node).Match(node,
            static (n, _) => n.CanHaveChildren,
            static (n, _) => n.CanHaveChildren,
            static (_, ready) => ready.Children.Count > 0,
            static (_, _) => true);
    }

    /// <summary>The child of <paramref name="parent"/> standing where <paramref name="other"/> stands under
    /// its own parent, naming the same thing: its counterpart under a copy of the same file read again.
    /// Null when there is none or more than one, or the children are not built yet; never blocks.</summary>
    public BrowseNode? FindLoadedChildLike(BrowseNode parent, BrowseNode other)
    {
        var children = PeekChildren(parent);
        if (children is not ChildrenState.Ready ready)
        {
            return null;
        }

        var segment = other.Segment;
        if (segment is null)
        {
            return null;
        }

        return FindChild(ready.Children, segment);
    }

    /// <summary>The one child among <paramref name="children"/> that <paramref name="segment"/> names;
    /// null when none or more than one does.</summary>
    internal static BrowseNode? FindChild(IReadOnlyList<BrowseNode> children, NodePathSegment segment)
    {
        BrowseNode? found = null;
        foreach (var child in children)
        {
            var childSegment = child.Segment;
            if (!segment.Equals(childSegment))
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = child;
        }
        return found;
    }

    /// <summary>The namespaces whose names extend <paramref name="node"/>'s by a segment, sorted by name;
    /// none for a node that is no namespace. Never blocks.</summary>
    public IReadOnlyList<BrowseNode> ListInnerNamespaces(BrowseNode node)
    {
        if (Inner.TryGetValue(node, out var known))
        {
            return known;
        }

        if (node.Subject is not NodeSubject.Namespace)
        {
            return [];
        }

        var parent = node.Parent;
        if (parent is null)
        {
            return [];
        }

        var parentChildren = PeekChildren(parent);
        if (parentChildren is not ChildrenState.Ready siblings)
        {
            return [];
        }

        return AddInnerNamespaces(node, siblings.Children);
    }

    /// <summary>Apart from <see cref="ListInnerNamespaces"/>, which the tree asks per row every frame while
    /// namespaces nest, so that only the first time pays for the closures.</summary>
    private IReadOnlyList<BrowseNode> AddInnerNamespaces(BrowseNode node, IReadOnlyList<BrowseNode> siblings)
    {
        var inner = siblings
            .Where(c => ReferenceEquals(c.EnclosingNamespace, node))
            .ToArray();
        return Inner.GetValue(node, _ => inner);
    }

    /// <summary>The children as the tree lists them when it nests namespaces, given those built: for a
    /// module, its children but the namespaces nested in others; for a namespace, the namespaces nested in
    /// it and its own children, each where ILSpy sorts the full names of the types in it; else as built.</summary>
    private IReadOnlyList<BrowseNode> NestChildren(BrowseNode node, IReadOnlyList<BrowseNode> children)
    {
        if (node.Subject is NodeSubject.Module)
        {
            return children.Where(static c => c.EnclosingNamespace is null).ToArray();
        }

        if (node.Subject is NodeSubject.Namespace)
        {
            return Interleave(ListInnerNamespaces(node), children);
        }

        return children;
    }

    /// <summary>The namespaces in <paramref name="inner"/> among <paramref name="own"/>, a namespace's
    /// children sorted.</summary>
    private static BrowseNode[] Interleave(IReadOnlyList<BrowseNode> inner, IReadOnlyList<BrowseNode> own)
    {
        var result = new List<BrowseNode>(own.Count + inner.Count);
        var pending = new Queue<BrowseNode>(inner);
        foreach (var child in own)
        {
            while (pending.TryPeek(out var next) && SortsBefore(next, child))
            {
                result.Add(pending.Dequeue());
            }

            result.Add(child);
        }
        result.AddRange(pending);
        return result.ToArray();
    }

    /// <summary>Whether ILSpy, sorting the full names of the types, comes to those in
    /// <paramref name="inner"/> before <paramref name="child"/>.</summary>
    private static bool SortsBefore(BrowseNode inner, BrowseNode child)
    {
        var key = NamespaceNames.LastSegment(inner.FullName) + ".";
        return NaturalStringComparer.Instance.Compare(key, GetReflectionName(child)) < 0;
    }

    /// <summary>The name a namespace's type is sorted under, as its reflection name ends: <c>List`1</c>.</summary>
    private static string GetReflectionName(BrowseNode child)
    {
        if (child.Subject is NodeSubject.TypeDefinition type)
        {
            return ReflectionName(type.Definition.Name, type.Definition.Arity);
        }

        if (child.Subject is NodeSubject.ForwardedType forwarded)
        {
            return ReflectionName(forwarded.Value.SortName, forwarded.Value.Arity);
        }

        throw new UnreachableException();
    }

    /// <summary>The reflection name of a type named <paramref name="name"/> with <paramref name="arity"/> type
    /// parameters: <c>List`1</c>.</summary>
    private static string ReflectionName(string name, int arity)
    {
        return arity == 0 ? name : name + "`" + arity;
    }
}
