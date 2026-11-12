using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ILSight.Core.Metadata;

namespace ILSight.Core;

/// <summary>Building and finding the nodes of a <see cref="BrowseSession"/>. All of this is thread-safe, and it is
/// what keeps the session so: its tables are safe to share, and where a node's children stand is
/// <see cref="Guarded{T}"/>.</summary>
public static class BrowseSessions
{
    private const string NamespaceIdPrefix = "N:";

    /// <summary>The root of <paramref name="assembly"/>'s tree. For an assembly read out of a package, it
    /// sits in the package's tree, under the folder holding its file.</summary>
    public static BrowseNode EnsureRoot(BrowseSession session, LoadedAssembly assembly)
    {
        if (assembly is LoadedModule module)
        {
            return EnsureRoot(session, module);
        }

        return session.OtherRoots.GetValue(assembly, static a => CreateRoot(a));
    }

    private static BrowseNode CreateRoot(LoadedAssembly assembly)
    {
        return assembly switch
        {
            FailedAssembly failed => ModuleNodes.CreateFailedRoot(failed),
            LoadedPackage package => PackageNodes.CreatePackageRoot(package),
            _ => throw new UnreachableException(),
        };
    }

    internal static BrowseNode EnsureRoot(BrowseSession session, LoadedModule module)
    {
        if (module.Place is null)
        {
            return session.ModuleRoots.GetValue(module, static m => ModuleNodes.CreateModuleRoot(m, null));
        }

        return EnsureRootInPackage(session, module, module.Place);
    }

    /// <summary>The root of <paramref name="module"/> under the folder of its package's tree holding its
    /// file: the one that folder lists, else one made there the first time, then kept.</summary>
    private static BrowseNode EnsureRootInPackage(BrowseSession session, LoadedModule module, PackagePlace place)
    {
        if (session.ModuleRoots.TryGetValue(module, out var placed))
        {
            return placed;
        }

        var packageRoot = EnsureRoot(session, place.Container);
        var folder = FindFolderNode(session, packageRoot, place.File.Folder) ?? packageRoot;
        var listing = GetChildren(session, folder);
        if (listing is ChildrenState.Ready && session.ModuleRoots.TryGetValue(module, out var listed))
        {
            return listed;
        }

        // The folder failed to list, or lists no root for the module: one is made there.
        return session.ModuleRoots.GetValue(module, m => ModuleNodes.CreateModuleRoot(m, folder));
    }

    /// <summary>The node of the folder at <paramref name="folder"/>, a path from the package's root: the
    /// folder's own node, or the node it is merged into; the package's root for its root. Null when the
    /// package holds no such folder.</summary>
    private static BrowseNode? FindFolderNode(BrowseSession session, BrowseNode packageRoot, string folder)
    {
        if (folder.Length == 0)
        {
            return packageRoot;
        }

        var node = packageRoot;
        while (true)
        {
            BrowseNode? next = null;
            foreach (var child in ListChildren(session, node))
            {
                if (child.Subject is not NodeSubject.Package package)
                {
                    continue;
                }

                if (PackageFolders.ShowsFolder(package.Value, folder))
                {
                    return child;
                }

                if (PackageFolders.IsWithin(folder, package.Value.Folder.FullName))
                {
                    next = child;
                    break;
                }
            }
            if (next is null)
            {
                return null;
            }

            node = next;
        }
    }

    /// <summary>Where <paramref name="node"/>'s children stand now; never blocks or builds them.</summary>
    public static ChildrenState PeekChildren(BrowseSession session, BrowseNode node)
    {
        if (!session.Children.TryGetValue(node, out var slot))
        {
            return ChildrenState.NotLoaded;
        }

        using var held = slot.Enter();
        return held.Value;
    }

    /// <summary><see cref="PeekChildren"/>, with the children as the tree lists them when it nests
    /// namespaces: for an assembly, its children but the namespaces nested in others; for a namespace,
    /// the namespaces nested in it and its own children, sorted together by name.</summary>
    public static ChildrenState PeekNestedChildren(BrowseSession session, BrowseNode node)
    {
        var state = PeekChildren(session, node);
        if (state is not ChildrenState.Ready ready)
        {
            return state;
        }

        if (session.Nested.TryGetValue(node, out var nested))
        {
            return new ChildrenState.Ready(nested);
        }

        return new ChildrenState.Ready(AddNested(session, node, ready.Children));
    }

    /// <summary>Apart from <see cref="PeekNestedChildren"/>, which the tree asks per expanded row every frame,
    /// so that only the first time pays for the closure.</summary>
    private static IReadOnlyList<BrowseNode> AddNested(BrowseSession session, BrowseNode node, IReadOnlyList<BrowseNode> children)
    {
        return session.Nested.GetValue(node, n => NestChildren(session, n, children));
    }

    /// <summary>The children of <paramref name="node"/>, built on the thread pool when nobody asked for
    /// them yet. Every caller waits for the same build; cancelling a wait leaves the build running for
    /// the others. Ends <see cref="ChildrenState.Ready"/> or <see cref="ChildrenState.Failed"/>.</summary>
    public static Task<ChildrenState> GetChildrenAsync(BrowseSession session, BrowseNode node, CancellationToken cancellationToken = default)
    {
        var slot = EnsureSlot(session, node);
        Task<ChildrenState> load;
        using (var held = slot.Enter())
        {
            switch (held.Value)
            {
                case ChildrenState.Ready or ChildrenState.Failed:
                    return Task.FromResult(held.Value);
                case ChildrenState.Running running:
                    load = running.Load;
                    break;
                case ChildrenState.Unloaded:
                    // Running before the build can keep what it built: it waits for the lock held here.
                    load = Task.Run(() => KeepBuilt(session, node, slot));
                    held.Value = new ChildrenState.Running(load);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
        return load.WaitAsync(cancellationToken);
    }

    /// <summary>The children of <paramref name="node"/>, built on this thread when nobody asked for them
    /// yet, else waited for while another builds them. Blocks: call it off the UI thread. Ends
    /// <see cref="ChildrenState.Ready"/> or <see cref="ChildrenState.Failed"/>.</summary>
    public static ChildrenState GetChildren(BrowseSession session, BrowseNode node)
    {
        var slot = EnsureSlot(session, node);
        TaskCompletionSource<ChildrenState>? building = null;
        Task<ChildrenState> load;
        using (var held = slot.Enter())
        {
            switch (held.Value)
            {
                case ChildrenState.Ready or ChildrenState.Failed:
                    return held.Value;
                case ChildrenState.Running running:
                    load = running.Load;
                    break;
                case ChildrenState.Unloaded:
                    building = new TaskCompletionSource<ChildrenState>(TaskCreationOptions.RunContinuationsAsynchronously);
                    held.Value = new ChildrenState.Running(building.Task);
                    load = building.Task;
                    break;
                default:
                    throw new UnreachableException();
            }
        }
        if (building is not null)
        {
            return FinishBuilding(session, node, slot, building);
        }

        return load.GetAwaiter().GetResult();
    }

    /// <summary>The children of <paramref name="node"/>, as <see cref="GetChildren"/> builds them; none
    /// when they failed to build. Blocks: call it off the UI thread.</summary>
    public static IReadOnlyList<BrowseNode> ListChildren(BrowseSession session, BrowseNode node)
    {
        var children = GetChildren(session, node);
        if (children is ChildrenState.Ready ready)
        {
            return ready.Children;
        }

        return [];
    }

    /// <summary>Whether <paramref name="node"/> has children, answered without building them where
    /// possible. Children that failed to build count, so that why can be shown.</summary>
    public static bool HasChildren(BrowseSession session, BrowseNode node)
    {
        return PeekChildren(session, node) switch
        {
            ChildrenState.Ready ready => ready.Children.Count > 0,
            ChildrenState.Failed => true,
            ChildrenState.Running or ChildrenState.Unloaded => BrowseNodes.CanHaveChildren(node),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The child of <paramref name="parent"/> standing where <paramref name="other"/> stands under
    /// its own parent, naming the same thing: its counterpart under a copy of the same file read again.
    /// Null when there is none or more than one, or the children are not built yet; never blocks.</summary>
    public static BrowseNode? FindLoadedChildLike(BrowseSession session, BrowseNode parent, BrowseNode other)
    {
        var children = PeekChildren(session, parent);
        if (children is not ChildrenState.Ready ready)
        {
            return null;
        }

        var segment = BrowseNodes.FindSegment(other);
        if (segment is null)
        {
            return null;
        }

        return FindChild(ready.Children, segment);
    }

    /// <summary>The one child among <paramref name="children"/> that <paramref name="segment"/> names;
    /// null when none or more than one does.</summary>
    private static BrowseNode? FindChild(IReadOnlyList<BrowseNode> children, NodePathSegment segment)
    {
        BrowseNode? found = null;
        foreach (var child in children)
        {
            var childSegment = BrowseNodes.FindSegment(child);
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

    private static Guarded<ChildrenState> EnsureSlot(BrowseSession session, BrowseNode node)
    {
        return session.Children.GetValue(node, static _ => new Guarded<ChildrenState>(ChildrenState.NotLoaded));
    }

    /// <summary>Builds the children, records the nodes they index, then hands them out to those waiting
    /// on <paramref name="building"/>.</summary>
    private static ChildrenState FinishBuilding(BrowseSession session, BrowseNode node, Guarded<ChildrenState> slot, TaskCompletionSource<ChildrenState> building)
    {
        var state = KeepBuilt(session, node, slot);
        building.SetResult(state);
        return state;
    }

    /// <summary>Builds the children, records the nodes they index, and keeps them in <paramref name="slot"/>.</summary>
    private static ChildrenState KeepBuilt(BrowseSession session, BrowseNode node, Guarded<ChildrenState> slot)
    {
        var state = BuildChildren(session, node);
        using (var held = slot.Enter())
        {
            held.Value = state;
        }

        return state;
    }

    private static ChildrenState BuildChildren(BrowseSession session, BrowseNode node)
    {
        BrowseNode[] built;
        try
        {
            built = BrowseNodes.CreateChildren(node);
        }
        catch (Exception e)
        {
            return new ChildrenState.Failed(e);
        }
        foreach (var child in built)
        {
            Register(session, child);
        }

        return new ChildrenState.Ready(built);
    }

    private static void Register(BrowseSession session, BrowseNode node)
    {
        if (node.Subject is NodeSubject.TypeDefinition type)
        {
            EnsureIndex(session, type.Definition.Module).TryAdd(type.Definition.Handle, node);
        }

        if (node.Subject is NodeSubject.MemberDefinition member)
        {
            EnsureIndex(session, member.Definition.Module).TryAdd(member.Definition.Handle, node);
        }

        if (node.Subject is NodeSubject.Module module)
        {
            session.ModuleRoots.TryAdd(module.Value, node);
        }

        if (node.Subject is NodeSubject.Namespace ns)
        {
            EnsureNamespaces(session, ns.Value.Module).TryAdd(ns.Value.Name, node);
        }
    }

    private static ConcurrentDictionary<EntityHandle, BrowseNode> EnsureIndex(BrowseSession session, LoadedModule module)
    {
        return session.Index.GetValue(module, static _ => new ConcurrentDictionary<EntityHandle, BrowseNode>());
    }

    /// <summary>The node <paramref name="path"/> leads to from <paramref name="item"/>'s root, when it is
    /// the item of the list the path starts at; else, or when nothing is there, null. May build the
    /// children on the way: call it off the UI thread.</summary>
    public static BrowseNode? FindNode(BrowseSession session, LoadedAssembly item, NodePath path)
    {
        if (!path.FilePath.Equals(item.FilePath))
        {
            return null;
        }

        return FollowSteps(session, EnsureRoot(session, item), path.Steps);
    }

    /// <summary>The node <paramref name="steps"/> lead to from <paramref name="root"/>; null when a step
    /// names no child, or more than one.</summary>
    private static BrowseNode? FollowSteps(BrowseSession session, BrowseNode root, ImmutableArray<NodePathStep> steps)
    {
        var node = root;
        foreach (var step in steps)
        {
            var segment = step.Segment;
            var next = segment.IsPackageEntry ? FindInPackage(session, node, segment) : FindChild(ListChildren(session, node), segment);
            if (next is null)
            {
                return null;
            }

            node = next;
        }
        return node;
    }

    /// <summary>The entry of the package whose root is <paramref name="packageRoot"/> that
    /// <paramref name="segment"/> names by its full path, wherever the folders holding it were merged.</summary>
    private static BrowseNode? FindInPackage(BrowseSession session, BrowseNode packageRoot, NodePathSegment segment)
    {
        if (packageRoot.Subject is not NodeSubject.Package package || !package.Value.IsRoot)
        {
            return null;
        }

        var path = segment switch
        {
            NodePathSegment.PackageFolder folder => folder.Path,
            NodePathSegment.PackageFile file => file.Path,
            NodePathSegment.EmbeddedModule module => module.Path,
            _ => throw new UnreachableException(),
        };
        if (segment is NodePathSegment.PackageFolder)
        {
            return FindFolderNode(session, packageRoot, path);
        }

        var holder = FindFolderNode(session, packageRoot, PackageFolders.FolderOf(path));
        if (holder is null)
        {
            return null;
        }

        return FindChild(ListChildren(session, holder), segment);
    }

    /// <summary>The node of the type or member <paramref name="handle"/> defines in <paramref name="module"/>;
    /// null for one the tree does not list.</summary>
    internal static BrowseNode? FindNode(BrowseSession session, LoadedModule module, EntityHandle handle)
    {
        if (handle.IsNil)
        {
            return null;
        }

        var index = EnsureIndex(session, module);
        if (index.TryGetValue(handle, out var node))
        {
            return node;
        }

        var parent = FindParent(session, module, handle);
        if (parent is null)
        {
            return null;
        }

        var siblings = GetChildren(session, parent);
        if (siblings is ChildrenState.Failed)
        {
            // The parent's children failed to build: nothing lists the node.
            return null;
        }

        return index.GetValueOrDefault(handle);
    }

    /// <summary>The node listing the node of <paramref name="handle"/>: a top-level type's namespace, else
    /// the node of the entity that holds it.</summary>
    private static BrowseNode? FindParent(BrowseSession session, LoadedModule module, EntityHandle handle)
    {
        if (handle.Kind != HandleKind.TypeDefinition)
        {
            return FindOwnerNode(session, module, handle);
        }

        var metadata = LoadedAssemblies.GetFile(module).Metadata;
        var type = metadata.GetTypeDefinition((TypeDefinitionHandle)handle);
        var declaringType = type.GetDeclaringType();
        if (!declaringType.IsNil)
        {
            return FindOwnerNode(session, module, handle);
        }

        return FindNamespace(session, module, metadata.GetString(type.Namespace));
    }

    /// <summary>The node of <paramref name="handle"/>, or of the nearest entity holding it that has one,
    /// as a compiler-generated member is held by the type it is generated in.</summary>
    internal static BrowseNode? FindOrAncestor(BrowseSession session, LoadedModule module, EntityHandle handle)
    {
        if (handle.IsNil)
        {
            return null;
        }

        EntityHandle? current = handle;
        while (current is not null)
        {
            var node = FindNode(session, module, current.Value);
            if (node is not null)
            {
                return node;
            }

            current = LoadedAssemblies.FindOwner(LoadedAssemblies.EnsureMainModule(module), current.Value);
        }
        return null;
    }

    /// <summary>The node of the entity that holds <paramref name="handle"/>'s; null when none holds it or it has no node.</summary>
    private static BrowseNode? FindOwnerNode(BrowseSession session, LoadedModule module, EntityHandle handle)
    {
        var owner = LoadedAssemblies.FindOwner(LoadedAssemblies.EnsureMainModule(module), handle);
        if (owner is null)
        {
            return null;
        }

        return FindNode(session, module, owner.Value);
    }

    private static BrowseNode? FindNamespace(BrowseSession session, LoadedModule module, string name)
    {
        var namespaces = GetChildren(session, EnsureRoot(session, module));
        if (namespaces is ChildrenState.Failed)
        {
            // The module's namespaces failed to build: none is listed.
            return null;
        }

        return EnsureNamespaces(session, module).GetValueOrDefault(name);
    }

    private static ConcurrentDictionary<string, BrowseNode> EnsureNamespaces(BrowseSession session, LoadedModule module)
    {
        return session.Namespaces.GetValue(module, static _ => new ConcurrentDictionary<string, BrowseNode>(StringComparer.Ordinal));
    }

    /// <summary>The namespaces whose names extend <paramref name="node"/>'s by a segment, sorted by name;
    /// none for a node that is no namespace. Never blocks.</summary>
    public static IReadOnlyList<BrowseNode> ListInnerNamespaces(BrowseSession session, BrowseNode node)
    {
        if (session.Inner.TryGetValue(node, out var known))
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

        var parentChildren = PeekChildren(session, parent);
        if (parentChildren is not ChildrenState.Ready siblings)
        {
            return [];
        }

        return AddInnerNamespaces(session, node, siblings.Children);
    }

    /// <summary>Apart from <see cref="ListInnerNamespaces"/>, which the tree asks per row every frame while
    /// namespaces nest, so that only the first time pays for the closures.</summary>
    private static IReadOnlyList<BrowseNode> AddInnerNamespaces(BrowseSession session, BrowseNode node, IReadOnlyList<BrowseNode> siblings)
    {
        var inner = siblings
            .Where(c => ReferenceEquals(c.EnclosingNamespace, node))
            .ToArray();
        return session.Inner.GetValue(node, _ => inner);
    }

    /// <summary>The children as the tree lists them when it nests namespaces, given those built: for a
    /// module, its children but the namespaces nested in others; for a namespace, the namespaces nested in
    /// it and its own children, each where ILSpy sorts the full names of the types in it; else as built.</summary>
    private static IReadOnlyList<BrowseNode> NestChildren(BrowseSession session, BrowseNode node, IReadOnlyList<BrowseNode> children)
    {
        return node.Subject switch
        {
            NodeSubject.Module => children.Where(static c => c.EnclosingNamespace is null).ToArray(),
            NodeSubject.Namespace => Interleave(ListInnerNamespaces(session, node), children),
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
        var key = NamespaceNames.LastSegment(BrowseNodes.GetFullName(inner)) + ".";
        return NaturalStringComparer.Instance.Compare(key, GetReflectionName(child)) < 0;
    }

    /// <summary>The name a namespace's type is sorted under, as its reflection name ends: <c>List`1</c>.</summary>
    private static string GetReflectionName(BrowseNode child)
    {
        var (name, arity) = child.Subject switch
        {
            NodeSubject.TypeDefinition type => (type.Definition.Symbol.Name, type.Definition.Symbol.TypeParameterCount),
            NodeSubject.ForwardedType forwarded => (forwarded.Value.SortName, forwarded.Value.Arity),
            _ => throw new UnreachableException(),
        };
        return arity == 0 ? name : name + "`" + arity;
    }

    /// <summary>The node of the namespace, type or member <paramref name="idString"/> names in
    /// <paramref name="module"/>, written as XML documentation ids are (<c>N:</c>, <c>T:</c>, <c>M:</c>,
    /// <c>P:</c>, <c>F:</c> or <c>E:</c> and the full name); null when it defines none such, or the id
    /// cannot be read.</summary>
    internal static BrowseNode? FindById(BrowseSession session, LoadedModule module, string idString)
    {
        if (idString.StartsWith(NamespaceIdPrefix, StringComparison.Ordinal))
        {
            return FindNamespace(session, module, idString.Substring(NamespaceIdPrefix.Length));
        }

        var entity = LoadedAssemblies.FindDefinedEntity(LoadedAssemblies.EnsureMainModule(module), idString);
        if (entity is null)
        {
            return null;
        }

        return FindOrAncestor(session, module, entity.MetadataToken);
    }

    /// <summary>The row <paramref name="handle"/> names in <paramref name="module"/>'s own metadata
    /// tables, or null when it names no table row or the tables could not be read.</summary>
    internal static MetadataTarget? FindMetadataRow(BrowseSession session, LoadedModule module, EntityHandle handle)
    {
        if (!MetadataTokens.TryGetTableIndex(handle.Kind, out var index))
        {
            return null;
        }

        var metadata = ListChildren(session, EnsureRoot(session, module))
            .FirstOrDefault(f => IsMetadataOf(f, LoadedAssemblies.GetFile(module)));
        if (metadata is null)
        {
            return null;
        }

        var table = FindTable(session, metadata, index);
        if (table is null)
        {
            return null;
        }

        var row = MetadataTokens.GetRowNumber(handle) - 1;
        return new MetadataTarget(table, row);
    }

    /// <summary>The table node and row that <paramref name="address"/>, a link of the grid
    /// <paramref name="grid"/> shows, leads to, among the tables of the metadata the grid lists; null
    /// when <paramref name="grid"/> lists no metadata, or that metadata lists no such table.</summary>
    public static MetadataTarget? FindMetadataRow(BrowseSession session, BrowseNode grid, MetadataAddress address)
    {
        var folder = FindMetadataFolder(grid);
        if (folder is null)
        {
            return null;
        }

        var table = FindTable(session, folder, address.Table);
        if (table is null)
        {
            return null;
        }

        return new MetadataTarget(table, address.Row);
    }

    /// <summary>The "Metadata" folder <paramref name="node"/> belongs to.</summary>
    private static BrowseNode? FindMetadataFolder(BrowseNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            var metadata = FindMetadata(parent);
            if (metadata is not null)
            {
                return parent;
            }
        }
        return null;
    }

    /// <summary>Whether <paramref name="node"/> is the folder of <paramref name="file"/>'s metadata.</summary>
    private static bool IsMetadataOf(BrowseNode node, ICSharpCode.Decompiler.Metadata.MetadataFile file)
    {
        var metadata = FindMetadata(node);
        if (metadata is null)
        {
            return false;
        }

        return ReferenceEquals(metadata.File, file);
    }

    /// <summary>The metadata <paramref name="node"/> is the folder of; null for a node of anything else.</summary>
    private static MetadataFolderInfo? FindMetadata(BrowseNode node)
    {
        if (node.Subject is not NodeSubject.Metadata metadata)
        {
            return null;
        }

        return metadata.Value;
    }

    /// <summary>The node listing the rows of <paramref name="table"/> in <paramref name="folder"/>, or null
    /// when it lists none.</summary>
    private static BrowseNode? FindTable(BrowseSession session, BrowseNode folder, TableIndex table)
    {
        var tables = ListChildren(session, folder).FirstOrDefault(static c => c.Subject is NodeSubject.MetadataTables);
        if (tables is null)
        {
            return null;
        }

        return ListChildren(session, tables).FirstOrDefault(c => c.Subject is NodeSubject.MetadataTable part
            && part.Value.ListedTable == table);
    }
}
