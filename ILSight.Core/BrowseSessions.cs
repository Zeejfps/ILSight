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

    private static async Task<BrowseNode> EnsureRootInPackageAsync(BrowseSession session, LoadedModule module, PackagePlace place, CancellationToken ct)
    {
        var placed = session.FindModuleRoot(module);
        if (placed is not null)
        {
            return placed;
        }

        var packageRoot = await EnsureRootAsync(session, place.Container, ct);
        var folder = await FindFolderNodeAsync(session, packageRoot, place.File.Folder, ct) ?? packageRoot;
        var listing = await GetChildrenAsync(session, folder, ct);
        if (listing is ChildrenState.Ready)
        {
            var listed = session.FindModuleRoot(module);
            if (listed is not null)
            {
                return listed;
            }
        }

        // The folder failed to list, or lists no root for the module: one is made there.
        return session.EnsureModuleRoot(module, folder);
    }

    // A folder may have no node of its own: the node it is merged into stands for it.
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
        var slot = session.EnsureChildrenSlot(node);
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

    private static async Task<ChildrenState> KeepBuiltAsync(BrowseSession session, BrowseNode node, Guarded<ChildrenState> slot, CancellationToken ct)
    {
        var state = await BuildChildrenAsync(session, node, ct);
        using (var held = slot.Enter())
        {
            held.Value = state;
        }

        return state;
    }

    private static async Task<ChildrenState> BuildChildrenAsync(BrowseSession session, BrowseNode node, CancellationToken ct)
    {
        // A node of a module released (closed) before the build began has no children to show, nor one whose children
        // are read from a module released.
        using var reading = LoadedAssemblies.BeginReading();
        if (node.Assembly is LoadedModule own && !LoadedAssemblies.TryRead(reading, own))
        {
            return new ChildrenState.Ready([]);
        }

        var module = BrowseNodes.FindModuleReadForChildren(node.Subject);
        if (module is not null)
        {
            var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, module, ct);
            return read.Match<ChildrenState>(
                r => BuildChildren(session, node, new ChildrenReads { Main = r.TypeSystem.MainModule }),
                static u => new ChildrenState.Failed(u.Thrown.SourceException),
                static _ => new ChildrenState.Ready([]));
        }

        var derivedOf = BrowseNodes.FindDerivedTypesModule(node.Subject);
        if (derivedOf is not null)
        {
            var reachable = await DerivedTypes.ReadReachableAsync(reading, derivedOf, ct);
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

        foreach (var child in built)
        {
            session.Register(child);
        }

        return new ChildrenState.Ready(built);
    }

    /// <summary>The node <paramref name="path"/> leads to from <paramref name="item"/>'s root, when it is
    /// the item of the list the path starts at; else, or when nothing is there, not found; unreadable when a step lists
    /// the children of a module whose type system the library could not read. May build the children on the way: call
    /// it off the UI thread.</summary>
    public static async Task<Lookup<BrowseNode>> FindNodeAsync(BrowseSession session, LoadedAssembly item, NodePath path, CancellationToken ct = default)
    {
        if (!path.FilePath.Equals(item.FilePath))
        {
            return new Lookup<BrowseNode>.NotFound();
        }

        return await FollowStepsAsync(session, await EnsureRootAsync(session, item, ct), path.Steps, ct);
    }

    private static async Task<Lookup<BrowseNode>> FollowStepsAsync(BrowseSession session, BrowseNode root, ImmutableArray<NodePathStep> steps,
        CancellationToken ct)
    {
        Lookup<BrowseNode> reached = new Lookup<BrowseNode>.Found { Value = root };
        foreach (var step in steps)
        {
            var segment = step.Segment;
            reached = await reached.ThenFindAsync((node, c) => FindStepAsync(session, node, segment, c), ct);
        }
        return reached;
    }

    private static async Task<Lookup<BrowseNode>> FindStepAsync(BrowseSession session, BrowseNode node, NodePathSegment segment, CancellationToken ct)
    {
        if (segment.IsPackageEntry)
        {
            return Lookup.FromNullable(await FindInPackageAsync(session, node, segment, ct));
        }

        var children = await ListReadableChildrenAsync(session, node, ct);
        return children.Match(
            done => Lookup.FromNullable(BrowseSession.FindChild(done.Value, segment)),
            static unreadable => new Lookup<BrowseNode>.Unreadable { Module = unreadable.Module },
            static _ => new Lookup<BrowseNode>.NotFound());
    }

    /// <summary>The children of <paramref name="node"/>, as <see cref="ListChildrenAsync"/> lists them, once the read
    /// of the type system their build reads ended (a module's, a type reference's or a module reference's module);
    /// that module, with why, when the library could not read it, and no children; closed when it was released (closed)
    /// before.</summary>
    internal static async Task<TypeSystemWork<IReadOnlyList<BrowseNode>>> ListReadableChildrenAsync(BrowseSession session, BrowseNode node,
        CancellationToken ct)
    {
        var module = BrowseNodes.FindModuleReadForChildren(node.Subject);
        if (module is null)
        {
            return new TypeSystemWork<IReadOnlyList<BrowseNode>>.Done { Value = await ListChildrenAsync(session, node, ct) };
        }

        using var reading = LoadedAssemblies.BeginReading();
        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, module, ct);
        return await read.Match<Task<TypeSystemWork<IReadOnlyList<BrowseNode>>>>(
            async _ => new TypeSystemWork<IReadOnlyList<BrowseNode>>.Done { Value = await ListChildrenAsync(session, node, ct) },
            u => Task.FromResult<TypeSystemWork<IReadOnlyList<BrowseNode>>>(new TypeSystemWork<IReadOnlyList<BrowseNode>>.Unreadable
            {
                Module = new UnreadableModule { Module = module, Why = u.Thrown.SourceException },
            }),
            static c => Task.FromResult<TypeSystemWork<IReadOnlyList<BrowseNode>>>(new TypeSystemWork<IReadOnlyList<BrowseNode>>.Closed { Module = c.Module }));
    }
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

        var node = session.FindIndexed(module, handle);
        if (node is not null)
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

        return session.FindIndexed(module, handle);
    }

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

        return session.FindNamespace(module, name);
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
    /// tables; not found when it names no table row or the tables could not be read; unreadable when the library could
    /// not read the module's type system, which listing the module's folders reads. The module is added to
    /// <paramref name="reading"/>, the unit's: not found when it was released (closed) before.</summary>
    internal static async Task<Lookup<MetadataTarget>> FindMetadataRowAsync(BrowseSession session, ModuleReading reading, LoadedModule module,
        EntityHandle handle, CancellationToken ct)
    {
        if (!MetadataTokens.TryGetTableIndex(handle.Kind, out var index) || !LoadedAssemblies.TryRead(reading, module))
        {
            return new Lookup<MetadataTarget>.NotFound();
        }

        var folders = await ListReadableChildrenAsync(session, await EnsureRootAsync(session, module, ct), ct);
        return await folders.Match(
            done => FindRowAsync(session, done.Value, LoadedAssemblies.GetFile(module), index, handle, ct),
            static unreadable => Task.FromResult<Lookup<MetadataTarget>>(new Lookup<MetadataTarget>.Unreadable { Module = unreadable.Module }),
            static _ => Task.FromResult<Lookup<MetadataTarget>>(new Lookup<MetadataTarget>.NotFound()));
    }

    private static async Task<Lookup<MetadataTarget>> FindRowAsync(BrowseSession session, IReadOnlyList<BrowseNode> folders,
        ICSharpCode.Decompiler.Metadata.MetadataFile file, TableIndex index, EntityHandle handle, CancellationToken ct)
    {
        var metadata = folders.FirstOrDefault(f => IsMetadataOf(f, file));
        if (metadata is null)
        {
            return new Lookup<MetadataTarget>.NotFound();
        }

        var table = await FindTableAsync(session, metadata, index, ct);
        if (table is null)
        {
            return new Lookup<MetadataTarget>.NotFound();
        }

        var row = MetadataTokens.GetRowNumber(handle) - 1;
        return new Lookup<MetadataTarget>.Found { Value = new MetadataTarget { Node = table, Row = row } };
    }

    private static bool IsMetadataOf(BrowseNode node, ICSharpCode.Decompiler.Metadata.MetadataFile file)
    {
        var metadata = FindMetadata(node);
        if (metadata is null)
        {
            return false;
        }

        return ReferenceEquals(metadata.File, file);
    }

    private static MetadataFolderInfo? FindMetadata(BrowseNode node)
    {
        if (node.Subject is not NodeSubject.Metadata metadata)
        {
            return null;
        }

        return metadata.Value;
    }

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
