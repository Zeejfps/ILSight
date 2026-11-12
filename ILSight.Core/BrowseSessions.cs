using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Metadata;

namespace ILSight.Core;

/// <summary>Building and finding the nodes of a <see cref="BrowseSession"/>. All of this is thread-safe, and it is
/// what keeps the session so: its tables are safe to share, and where a node's children stand is
/// <see cref="Guarded{T}"/>.</summary>
public static class BrowseSessions
{
    private const string NamespaceIdPrefix = "N:";

    /// <summary>The root of <paramref name="assembly"/>'s tree. For an assembly read out of a package, it
    /// sits in the package's tree, under the folder holding its file, which may be built to find it.</summary>
    public static async Task<BrowseNode> EnsureRootAsync(BrowseSession session, LoadedAssembly assembly, CancellationToken ct = default)
    {
        if (assembly is LoadedModule module && module.Place is not null)
        {
            return await EnsureRootInPackageAsync(session, module, module.Place, ct);
        }

        return session.EnsureRoot(assembly);
    }

    /// <summary>The root of <paramref name="module"/> under the folder of its package's tree holding its
    /// file: the one that folder lists, else one made there the first time, then kept.</summary>
    private static async Task<BrowseNode> EnsureRootInPackageAsync(BrowseSession session, LoadedModule module, PackagePlace place, CancellationToken ct)
    {
        if (session.ModuleRoots.TryGetValue(module, out var placed))
        {
            return placed;
        }

        var packageRoot = await EnsureRootAsync(session, place.Container, ct);
        var folder = await FindFolderNodeAsync(session, packageRoot, place.File.Folder, ct) ?? packageRoot;
        var listing = await GetChildrenAsync(session, folder, ct);
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
    private static async Task<BrowseNode?> FindFolderNodeAsync(BrowseSession session, BrowseNode packageRoot, string folder, CancellationToken ct)
    {
        if (folder.Length == 0)
        {
            return packageRoot;
        }

        var node = packageRoot;
        while (true)
        {
            BrowseNode? next = null;
            foreach (var child in await ListChildrenAsync(session, node, ct))
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

    /// <summary>The children of <paramref name="node"/>, built on the thread pool when nobody asked for them yet, once
    /// the read of the type system the build reads ended (a module's, a type reference's or a module reference's
    /// module: a module the library could not read fails the build, saying why).
    /// The caller that starts the build awaits it, to its end, whatever its token says, so no build runs that nobody
    /// awaits; a caller that finds it running waits for it until <paramref name="cancellationToken"/> fires. Ends
    /// <see cref="ChildrenState.Ready"/> or <see cref="ChildrenState.Failed"/>.</summary>
    public static Task<ChildrenState> GetChildrenAsync(BrowseSession session, BrowseNode node, CancellationToken cancellationToken = default)
    {
        var slot = EnsureSlot(session, node);
        Task<ChildrenState> load;
        using (var held = slot.Enter())
        {
            // Running before the build can keep what it built: it waits for the lock held here.
            // The build runs to its end whatever the token says: whoever waits next takes it.
            var (found, started) = held.Value.Match((session, node, slot),
                static (s, _) => (Task.Run(() => KeepBuiltAsync(s.session, s.node, s.slot, CancellationToken.None)), true),
                static (_, running) => (running.Load, false),
                static (_, ready) => (Task.FromResult<ChildrenState>(ready), false),
                static (_, failed) => (Task.FromResult<ChildrenState>(failed), false));
            if (started)
            {
                held.Value = new ChildrenState.Running(found);
                return found;
            }

            load = found;
        }
        return load.WaitAsync(cancellationToken);
    }

    /// <summary>The children of <paramref name="node"/>, as <see cref="GetChildrenAsync"/> builds them; none
    /// when they failed to build.</summary>
    public static async Task<IReadOnlyList<BrowseNode>> ListChildrenAsync(BrowseSession session, BrowseNode node, CancellationToken ct = default)
    {
        var children = await GetChildrenAsync(session, node, ct);
        if (children is ChildrenState.Ready ready)
        {
            return ready.Children;
        }

        return [];
    }

    private static Guarded<ChildrenState> EnsureSlot(BrowseSession session, BrowseNode node)
    {
        return session.Children.GetValue(node, static _ => new Guarded<ChildrenState>(ChildrenState.NotLoaded));
    }

    /// <summary>Builds the children once the read of the type system the build reads ended, records the nodes they
    /// index, and keeps them in <paramref name="slot"/>.</summary>
    private static async Task<ChildrenState> KeepBuiltAsync(BrowseSession session, BrowseNode node, Guarded<ChildrenState> slot, CancellationToken ct)
    {
        var state = await BuildChildrenAsync(session, node, ct);
        using (var held = slot.Enter())
        {
            held.Value = state;
        }

        return state;
    }

    /// <summary>The children of <paramref name="node"/>, built once the reads of the type systems the build reads
    /// ended, which the build is handed; the build fails, saying why, for a module the library could not read.</summary>
    private static async Task<ChildrenState> BuildChildrenAsync(BrowseSession session, BrowseNode node, CancellationToken ct)
    {
        var module = BrowseNodes.FindModuleReadForChildren(node.Subject);
        if (module is not null)
        {
            var read = await LoadedAssemblies.ReadTypeSystemAsync(module, ct);
            return read.Match<ChildrenState>(
                r => BuildChildren(session, node, new ChildrenReads { Main = r.TypeSystem.MainModule }),
                static u => new ChildrenState.Failed(u.Thrown.SourceException));
        }

        var derivedOf = BrowseNodes.FindDerivedTypesModule(node.Subject);
        if (derivedOf is not null)
        {
            var reachable = await DerivedTypes.ReadReachableAsync(derivedOf, ct);
            return BuildChildren(session, node, new ChildrenReads { Reachable = reachable });
        }

        return BuildChildren(session, node, ChildrenReads.None);
    }

    private static ChildrenState BuildChildren(BrowseSession session, BrowseNode node, ChildrenReads reads)
    {
        BrowseNode[] built;
        try
        {
            built = BrowseNodes.CreateChildren(node, reads);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new ChildrenState.Failed(e);
        }

        var unreadable = reads.Unreadable.Values.FirstOrDefault();
        if (unreadable is not null)
        {
            // The tree has no row to name a module the search for derived types couldn't read: the build fails, saying
            // why, as it did when the read threw.
            return new ChildrenState.Failed(unreadable.Why);
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
    public static async Task<BrowseNode?> FindNodeAsync(BrowseSession session, LoadedAssembly item, NodePath path, CancellationToken ct = default)
    {
        if (!path.FilePath.Equals(item.FilePath))
        {
            return null;
        }

        return await FollowStepsAsync(session, await EnsureRootAsync(session, item, ct), path.Steps, ct);
    }

    /// <summary>The node <paramref name="steps"/> lead to from <paramref name="root"/>; null when a step
    /// names no child, or more than one.</summary>
    private static async Task<BrowseNode?> FollowStepsAsync(BrowseSession session, BrowseNode root, ImmutableArray<NodePathStep> steps, CancellationToken ct)
    {
        var node = root;
        foreach (var step in steps)
        {
            var segment = step.Segment;
            var next = await FindStepAsync(session, node, segment, ct);
            if (next is null)
            {
                return null;
            }

            node = next;
        }
        return node;
    }

    /// <summary>The child of <paramref name="node"/> <paramref name="segment"/> names: an entry of a package by its
    /// full path, else the one child it names.</summary>
    private static async Task<BrowseNode?> FindStepAsync(BrowseSession session, BrowseNode node, NodePathSegment segment, CancellationToken ct)
    {
        if (segment.IsPackageEntry)
        {
            return await FindInPackageAsync(session, node, segment, ct);
        }

        return BrowseSession.FindChild(await ListChildrenAsync(session, node, ct), segment);
    }

    /// <summary>The entry of the package whose root is <paramref name="packageRoot"/> that
    /// <paramref name="segment"/> names by its full path, wherever the folders holding it were merged.</summary>
    private static async Task<BrowseNode?> FindInPackageAsync(BrowseSession session, BrowseNode packageRoot, NodePathSegment segment, CancellationToken ct)
    {
        if (packageRoot.Subject is not NodeSubject.Package package || !package.Value.IsRoot)
        {
            return null;
        }

        var path = PackagePathOf(segment);
        if (segment is NodePathSegment.PackageFolder)
        {
            return await FindFolderNodeAsync(session, packageRoot, path, ct);
        }

        var holder = await FindFolderNodeAsync(session, packageRoot, PackageFolders.FolderOf(path), ct);
        if (holder is null)
        {
            return null;
        }

        return BrowseSession.FindChild(await ListChildrenAsync(session, holder, ct), segment);
    }

    /// <summary>The full path in its package of the entry <paramref name="segment"/> names, a folder, a file or
    /// an assembly read out of the package.</summary>
    private static string PackagePathOf(NodePathSegment segment)
    {
        if (segment is NodePathSegment.PackageFolder folder)
        {
            return folder.Path;
        }

        if (segment is NodePathSegment.PackageFile file)
        {
            return file.Path;
        }

        if (segment is NodePathSegment.EmbeddedModule module)
        {
            return module.Path;
        }

        throw new UnreachableException();
    }

    /// <summary>The node of the type or member <paramref name="handle"/> defines in <paramref name="module"/>, whose
    /// type system read is <paramref name="main"/>'s; null for one the tree does not list.</summary>
    internal static async Task<BrowseNode?> FindNodeAsync(BrowseSession session, LoadedModule module, MetadataModule main, EntityHandle handle,
        CancellationToken ct)
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

        var parent = await FindParentAsync(session, module, main, handle, ct);
        if (parent is null)
        {
            return null;
        }

        var siblings = await GetChildrenAsync(session, parent, ct);
        if (siblings is ChildrenState.Failed)
        {
            // The parent's children failed to build: nothing lists the node.
            return null;
        }

        return index.GetValueOrDefault(handle);
    }

    /// <summary>The node listing the node of <paramref name="handle"/>: a top-level type's namespace, else
    /// the node of the entity that holds it.</summary>
    private static async Task<BrowseNode?> FindParentAsync(BrowseSession session, LoadedModule module, MetadataModule main, EntityHandle handle,
        CancellationToken ct)
    {
        if (handle.Kind != HandleKind.TypeDefinition)
        {
            return await FindOwnerNodeAsync(session, module, main, handle, ct);
        }

        var metadata = LoadedAssemblies.GetFile(module).Metadata;
        var type = metadata.GetTypeDefinition((TypeDefinitionHandle)handle);
        var declaringType = type.GetDeclaringType();
        if (!declaringType.IsNil)
        {
            return await FindOwnerNodeAsync(session, module, main, handle, ct);
        }

        return await FindNamespaceAsync(session, module, metadata.GetString(type.Namespace), ct);
    }

    /// <summary>The node of <paramref name="handle"/>, or of the nearest entity holding it that has one,
    /// as a compiler-generated member is held by the type it is generated in; <paramref name="main"/> is the
    /// module's type system as read.</summary>
    internal static async Task<BrowseNode?> FindOrAncestorAsync(BrowseSession session, LoadedModule module, MetadataModule main, EntityHandle handle,
        CancellationToken ct)
    {
        if (handle.IsNil)
        {
            return null;
        }

        EntityHandle? current = handle;
        while (current is not null)
        {
            var node = await FindNodeAsync(session, module, main, current.Value, ct);
            if (node is not null)
            {
                return node;
            }

            current = LoadedAssemblies.FindOwner(main, current.Value);
        }
        return null;
    }

    /// <summary>The node of the entity that holds <paramref name="handle"/>'s; null when none holds it or it has no node.</summary>
    private static async Task<BrowseNode?> FindOwnerNodeAsync(BrowseSession session, LoadedModule module, MetadataModule main, EntityHandle handle,
        CancellationToken ct)
    {
        var owner = LoadedAssemblies.FindOwner(main, handle);
        if (owner is null)
        {
            return null;
        }

        return await FindNodeAsync(session, module, main, owner.Value, ct);
    }

    private static async Task<BrowseNode?> FindNamespaceAsync(BrowseSession session, LoadedModule module, string name, CancellationToken ct)
    {
        var namespaces = await GetChildrenAsync(session, await EnsureRootAsync(session, module, ct), ct);
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

    /// <summary>The node of the namespace, type or member <paramref name="idString"/> names in
    /// <paramref name="module"/>, whose type system read is <paramref name="main"/>'s, written as XML documentation ids
    /// are (<c>N:</c>, <c>T:</c>, <c>M:</c>, <c>P:</c>, <c>F:</c> or <c>E:</c> and the full name); null when it defines
    /// none such, or the id cannot be read.</summary>
    internal static async Task<BrowseNode?> FindByIdAsync(BrowseSession session, LoadedModule module, MetadataModule main, string idString,
        CancellationToken ct)
    {
        if (idString.StartsWith(NamespaceIdPrefix, StringComparison.Ordinal))
        {
            return await FindNamespaceAsync(session, module, idString.Substring(NamespaceIdPrefix.Length), ct);
        }

        var entity = LoadedAssemblies.FindDefinedEntity(main, idString);
        if (entity is null)
        {
            return null;
        }

        return await FindOrAncestorAsync(session, module, main, entity.MetadataToken, ct);
    }

    /// <summary>The row <paramref name="handle"/> names in <paramref name="module"/>'s own metadata
    /// tables, or null when it names no table row or the tables could not be read.</summary>
    internal static async Task<MetadataTarget?> FindMetadataRowAsync(BrowseSession session, LoadedModule module, EntityHandle handle, CancellationToken ct)
    {
        if (!MetadataTokens.TryGetTableIndex(handle.Kind, out var index))
        {
            return null;
        }

        var file = LoadedAssemblies.GetFile(module);
        var metadata = (await ListChildrenAsync(session, await EnsureRootAsync(session, module, ct), ct))
            .FirstOrDefault(f => IsMetadataOf(f, file));
        if (metadata is null)
        {
            return null;
        }

        var table = await FindTableAsync(session, metadata, index, ct);
        if (table is null)
        {
            return null;
        }

        var row = MetadataTokens.GetRowNumber(handle) - 1;
        return new MetadataTarget { Node = table, Row = row };
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
    private static async Task<BrowseNode?> FindTableAsync(BrowseSession session, BrowseNode folder, TableIndex table, CancellationToken ct)
    {
        var tables = (await ListChildrenAsync(session, folder, ct)).FirstOrDefault(static c => c.Subject is NodeSubject.MetadataTables);
        if (tables is null)
        {
            return null;
        }

        return (await ListChildrenAsync(session, tables, ct)).FirstOrDefault(c => c.Subject is NodeSubject.MetadataTable part
            && part.Value.ListedTable == table);
    }
}
