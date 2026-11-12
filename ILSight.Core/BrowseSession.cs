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
        return PeekChildren(node) switch
        {
            ChildrenState.Ready ready => ready.Children.Count > 0,
            ChildrenState.Failed => true,
            ChildrenState.Running or ChildrenState.Unloaded => node.CanHaveChildren,
            _ => throw new UnreachableException(),
        };
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
        return node.Subject switch
        {
            NodeSubject.Module => children.Where(static c => c.EnclosingNamespace is null).ToArray(),
            NodeSubject.Namespace => Interleave(ListInnerNamespaces(node), children),
            _ => children,
        };
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
        var (name, arity) = child.Subject switch
        {
            NodeSubject.TypeDefinition type => (type.Definition.Name, type.Definition.Arity),
            NodeSubject.ForwardedType forwarded => (forwarded.Value.SortName, forwarded.Value.Arity),
            _ => throw new UnreachableException(),
        };
        return arity == 0 ? name : name + "`" + arity;
    }
}
