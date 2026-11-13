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
    private readonly ConditionalWeakTable<LoadedModule, BrowseNode> _moduleRoots = new();
    private readonly ConditionalWeakTable<LoadedModule, ConcurrentDictionary<string, BrowseNode>> _namespaces = new();
    private readonly ConditionalWeakTable<BrowseNode, IReadOnlyList<BrowseNode>> _nested = new();
    private readonly ConditionalWeakTable<BrowseNode, IReadOnlyList<BrowseNode>> _inner = new();
    private readonly ConditionalWeakTable<LoadedAssembly, BrowseNode> _otherRoots = new();
    private readonly ConditionalWeakTable<LoadedModule, ConcurrentDictionary<EntityHandle, BrowseNode>> _index = new();

    private readonly ConditionalWeakTable<BrowseNode, Guarded<ChildrenState>> _children = new();

    /// <summary>The root of the listed <paramref name="item"/>'s tree (a module not read out of a package, a package,
    /// a file that could not be read), made from the item's values the first time, then kept. Reads no file. A module
    /// read out of a package is no listed item: its root sits in its package's tree
    /// (<see cref="BrowseSessions.EnsureRootAsync"/>).</summary>
    public BrowseNode EnsureRoot(LoadedAssembly item)
    {
        if (item is LoadedModule module)
        {
            if (module.Place is not null)
            {
                throw new ArgumentException($"{module.Name} is read out of a package, which is the listed item.", nameof(item));
            }

            return _moduleRoots.GetValue(module, static m => BrowseRoots.Create(m));
        }

        return _otherRoots.GetValue(item, static a => BrowseRoots.Create(a));
    }

    /// <summary>The root kept for <paramref name="module"/>, a module read out of a package, listed or made; null before
    /// either.</summary>
    internal BrowseNode? FindModuleRoot(LoadedModule module)
    {
        if (_moduleRoots.TryGetValue(module, out var root))
        {
            return root;
        }

        return null;
    }

    /// <summary>The root kept for <paramref name="module"/>, else one made under <paramref name="folder"/> and kept.</summary>
    internal BrowseNode EnsureModuleRoot(LoadedModule module, BrowseNode folder)
    {
        return _moduleRoots.GetValue(module, m => ModuleNodes.CreateModuleRoot(m, folder));
    }

    /// <summary>Where <paramref name="node"/>'s children stand, made not loaded the first time it is asked for.</summary>
    internal Guarded<ChildrenState> EnsureChildrenSlot(BrowseNode node)
    {
        return _children.GetValue(node, static _ => new Guarded<ChildrenState>(ChildrenState.NotLoaded));
    }

    /// <summary>Keeps <paramref name="node"/>, a child just built, where it is found again: a type or member by its
    /// handle, a module's root by its module, a namespace by its name; the first one kept stays.</summary>
    internal void Register(BrowseNode node)
    {
        if (node.Subject is NodeSubject.TypeDefinition type)
        {
            EnsureIndex(type.Definition.Module).TryAdd(type.Definition.Handle, node);
        }

        if (node.Subject is NodeSubject.MemberDefinition member)
        {
            EnsureIndex(member.Definition.Module).TryAdd(member.Definition.Handle, node);
        }

        if (node.Subject is NodeSubject.Module module)
        {
            _moduleRoots.TryAdd(module.Value, node);
        }

        if (node.Subject is NodeSubject.Namespace ns)
        {
            EnsureNamespaces(ns.Value.Module).TryAdd(ns.Value.Name, node);
        }
    }

    /// <summary>The node kept for the type or member <paramref name="handle"/> names in <paramref name="module"/>; null
    /// when none was built yet.</summary>
    internal BrowseNode? FindIndexed(LoadedModule module, EntityHandle handle)
    {
        return EnsureIndex(module).GetValueOrDefault(handle);
    }

    /// <summary>The node kept for the namespace <paramref name="name"/> of <paramref name="module"/>; null when none
    /// was built yet.</summary>
    internal BrowseNode? FindNamespace(LoadedModule module, string name)
    {
        return EnsureNamespaces(module).GetValueOrDefault(name);
    }

    private ConcurrentDictionary<EntityHandle, BrowseNode> EnsureIndex(LoadedModule module)
    {
        return _index.GetValue(module, static _ => new ConcurrentDictionary<EntityHandle, BrowseNode>());
    }

    private ConcurrentDictionary<string, BrowseNode> EnsureNamespaces(LoadedModule module)
    {
        return _namespaces.GetValue(module, static _ => new ConcurrentDictionary<string, BrowseNode>(StringComparer.Ordinal));
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

    private BrowseNode? FindBuiltChild(BrowseNode node, Func<BrowseNode, bool> match)
    {
        var children = PeekChildren(node);
        if (children is not ChildrenState.Ready ready)
        {
            return null;
        }

        return ready.Children.FirstOrDefault(match);
    }

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

    // Kept as the tree's build keeps them: a read that fails is kept as their failure, which the tree shows.
    private IReadOnlyList<BrowseNode> EnsureTables(BrowseNode tables, MetadataFolderInfo folder)
    {
        var slot = EnsureChildrenSlot(tables);
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
        if (!_children.TryGetValue(node, out var slot))
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

        if (_nested.TryGetValue(node, out var nested))
        {
            return new ChildrenState.Ready(nested);
        }

        return new ChildrenState.Ready(AddNested(node, ready.Children));
    }

    // Apart from PeekNestedChildren, which the tree asks per expanded row every frame, so that only the first time pays
    // for the closure.
    private IReadOnlyList<BrowseNode> AddNested(BrowseNode node, IReadOnlyList<BrowseNode> children)
    {
        return _nested.GetValue(node, n => NestChildren(n, children));
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
        if (_inner.TryGetValue(node, out var known))
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

    // Apart from ListInnerNamespaces, which the tree asks per row every frame while namespaces nest, so that only the
    // first time pays for the closures.
    private IReadOnlyList<BrowseNode> AddInnerNamespaces(BrowseNode node, IReadOnlyList<BrowseNode> siblings)
    {
        var inner = siblings
            .Where(c => ReferenceEquals(c.EnclosingNamespace, node))
            .ToArray();
        return _inner.GetValue(node, _ => inner);
    }

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

    // ILSpy sorts the full names of the types (as their reflection names end: List`1), so an inner namespace goes
    // where the names of the types in it would sort.
    private static bool SortsBefore(BrowseNode inner, BrowseNode child)
    {
        var key = NamespaceNames.LastSegment(inner.FullName) + ".";
        return NaturalStringComparer.Instance.Compare(key, GetReflectionName(child)) < 0;
    }

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

    private static string ReflectionName(string name, int arity)
    {
        return arity == 0 ? name : name + "`" + arity;
    }
}
