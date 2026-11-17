using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.MetadataArea;

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

    // A folder may have no node of its own: the node it is merged into stands for it. None when it isn't there, or a
    // folder on the way failed to list.
    private static async Task<BrowseNode?> FindFolderNodeAsync(BrowseSession session, BrowseNode packageRoot, string folder, CancellationToken ct)
    {
        var found = await FindFolderAsync(session, packageRoot, folder, ct);
        return found is Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.Found node } ? node.Value : null;
    }

    // The folder's node, as FindFolderNodeAsync finds it; the failure, with why, of a folder on the way whose children
    // failed to build, never not found for what they could hold.
    private static async Task<Outcome<Lookup<BrowseNode>>> FindFolderAsync(BrowseSession session, BrowseNode packageRoot, string folder,
        CancellationToken ct)
    {
        if (folder.Length == 0)
        {
            return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Found { Value = packageRoot });
        }

        var node = packageRoot;
        while (true)
        {
            var children = await GetChildrenAsync(session, node, ct);
            if (children is ChildrenState.Failed failed)
            {
                return new Outcome<Lookup<BrowseNode>>.Failed(failed.Error);
            }

            var (shown, within) = FolderStepAmong(((ChildrenState.Ready)children).Children, folder);
            if (shown is not null)
            {
                return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Found { Value = shown });
            }

            if (within is null)
            {
                return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound());
            }

            node = within;
        }
    }

    // Among a package folder's children, the child showing the folder, or else the child the folder lies within.
    private static (BrowseNode? Shown, BrowseNode? Within) FolderStepAmong(IReadOnlyList<BrowseNode> children, string folder)
    {
        foreach (var child in children)
        {
            if (child.Subject is not NodeSubject.Package package)
            {
                continue;
            }

            if (PackageFolders.ShowsFolder(package.Value, folder))
            {
                return (child, null);
            }

            if (PackageFolders.IsWithin(folder, package.Value.Folder.FullName))
            {
                return (null, child);
            }
        }
        return (null, null);
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

    // Only what the library threw at one of the listing's reads fails the children (ListingReads); the app's own bug
    // goes on to the edge.
    private static ChildrenState BuildChildren(BrowseSession session, BrowseNode node, ChildrenReads reads)
    {
        BrowseNode[] built;
        try
        {
            built = BrowseNodes.CreateChildren(node, reads);
        }
        catch (ListingReadException e)
        {
            return new ChildrenState.Failed(e.Error);
        }

        foreach (var child in built)
        {
            session.Register(child);
        }

        return new ChildrenState.Ready(built);
    }

    /// <summary>The node <paramref name="path"/> leads to from <paramref name="item"/>'s root, when it is
    /// the item of the list the path starts at; else, or when nothing is there, not found; unreadable when a step lists
    /// the children of a module whose type system the library could not read; the failure, with why, of children a step
    /// lists that failed to build, never not found for what they could hold. May build the children on the way: call
    /// it off the UI thread.</summary>
    public static async Task<Outcome<Lookup<BrowseNode>>> FindNodeAsync(BrowseSession session, LoadedAssembly item, NodePath path,
        CancellationToken ct = default)
    {
        if (!path.FilePath.Equals(item.FilePath))
        {
            return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound());
        }

        return await WalkStepsAsync(session, await EnsureRootAsync(session, item, ct), path.Steps, ct);
    }

    private static async Task<Outcome<Lookup<BrowseNode>>> WalkStepsAsync(BrowseSession session, BrowseNode root,
        ImmutableArray<NodePathStep> steps, CancellationToken ct)
    {
        Outcome<Lookup<BrowseNode>> reached = new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Found { Value = root });
        foreach (var step in steps)
        {
            if (reached is not Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.Found node })
            {
                return reached;
            }

            reached = await FindStepAsync(session, node.Value, step.Segment, ct);
        }
        return reached;
    }

    private static async Task<Outcome<Lookup<BrowseNode>>> FindStepAsync(BrowseSession session, BrowseNode node, NodePathSegment segment,
        CancellationToken ct)
    {
        if (segment.IsPackageEntry)
        {
            return await FindInPackageAsync(session, node, segment, ct);
        }

        var children = await ReadChildrenAsync(session, node, ct);
        return FindAmongReadable(children, segment, static (listed, s) => BrowseSession.FindChild(listed, s));
    }

    /// <summary>What a lookup of one target finds among <paramref name="children"/>: the child <paramref name="pick"/>
    /// picks, not found when it picks none; the children's failure, with why, when they failed to build, never not
    /// found for what they could hold.</summary>
    internal static Outcome<Lookup<BrowseNode>> FindAmong<TState>(ChildrenState children, TState state,
        Func<IReadOnlyList<BrowseNode>, TState, BrowseNode?> pick)
    {
        if (children is ChildrenState.Failed failed)
        {
            return new Outcome<Lookup<BrowseNode>>.Failed(failed.Error);
        }

        return new Outcome<Lookup<BrowseNode>>.Ok(Lookup.FromNullable(pick(((ChildrenState.Ready)children).Children, state)));
    }

    /// <summary>As <see cref="FindAmong{TState}"/>, among children read as <see cref="ReadChildrenAsync"/> reads them:
    /// unreadable when the module their build reads couldn't be read, not found when it was released (closed).</summary>
    internal static Outcome<Lookup<BrowseNode>> FindAmongReadable<TState>(TypeSystemWork<ChildrenState> children, TState state,
        Func<IReadOnlyList<BrowseNode>, TState, BrowseNode?> pick)
    {
        return children.Match(
            done => FindAmong(done.Value, state, pick),
            static unreadable => new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.Unreadable { Module = unreadable.Module }),
            static _ => new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound()));
    }

    /// <summary>Where the children of <paramref name="node"/> stand once built, as <see cref="GetChildrenAsync"/> builds
    /// them, after the read of the type system their build reads ended (a module's, a type reference's or a module
    /// reference's module); that module, with why, when the library could not read it; closed when it was released
    /// (closed) before.</summary>
    internal static async Task<TypeSystemWork<ChildrenState>> ReadChildrenAsync(BrowseSession session, BrowseNode node, CancellationToken ct)
    {
        var module = BrowseNodes.FindModuleReadForChildren(node.Subject);
        if (module is null)
        {
            return new TypeSystemWork<ChildrenState>.Done { Value = await GetChildrenAsync(session, node, ct) };
        }

        using var reading = LoadedAssemblies.BeginReading();
        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, module, ct);
        return await read.Match<Task<TypeSystemWork<ChildrenState>>>(
            async _ => new TypeSystemWork<ChildrenState>.Done { Value = await GetChildrenAsync(session, node, ct) },
            u => Task.FromResult<TypeSystemWork<ChildrenState>>(new TypeSystemWork<ChildrenState>.Unreadable
            {
                Module = new UnreadableModule { Module = module, Why = u.Thrown.SourceException },
            }),
            static c => Task.FromResult<TypeSystemWork<ChildrenState>>(new TypeSystemWork<ChildrenState>.Closed { Module = c.Module }));
    }

    private static async Task<Outcome<Lookup<BrowseNode>>> FindInPackageAsync(BrowseSession session, BrowseNode packageRoot,
        NodePathSegment segment, CancellationToken ct)
    {
        if (packageRoot.Subject is not NodeSubject.Package package || !package.Value.IsRoot)
        {
            return new Outcome<Lookup<BrowseNode>>.Ok(new Lookup<BrowseNode>.NotFound());
        }

        var path = PackagePathOf(segment);
        if (segment is NodePathSegment.PackageFolder)
        {
            return await FindFolderAsync(session, packageRoot, path, ct);
        }

        var holder = await FindFolderAsync(session, packageRoot, PackageFolders.FolderOf(path), ct);
        if (holder is not Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.Found folder })
        {
            return holder;
        }

        return FindAmong(await GetChildrenAsync(session, folder.Value, ct), segment, static (listed, s) => BrowseSession.FindChild(listed, s));
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

    /// <summary>Where the type or member <paramref name="handle"/> defines in <paramref name="module"/> sits in the
    /// tree, whose type system read is <paramref name="main"/>'s: its node, none for one the tree does not list, or the
    /// failure of the children that would list it; the module unreadable, with why, when the library failed reading its
    /// metadata on the way (each read under its own catch, the next step chosen after it).</summary>
    internal static async Task<NodePlace> PlaceNodeAsync(BrowseSession session, LoadedModule module, MetadataModule main, EntityHandle handle,
        CancellationToken ct)
    {
        if (handle.IsNil)
        {
            return NodePlace.None;
        }

        var node = session.FindIndexed(module, handle);
        if (node is not null)
        {
            return new NodePlace.Listed(node);
        }

        var parent = await PlaceParentAsync(session, module, main, handle, ct);
        if (parent is not NodePlace.Listed listed)
        {
            return parent;
        }

        var siblings = await GetChildrenAsync(session, listed.Node, ct);
        if (siblings is ChildrenState.Failed failed)
        {
            // The parent's children failed to build: nothing lists the node.
            return new NodePlace.Failed(failed.Error);
        }

        var found = session.FindIndexed(module, handle);
        return found is null ? NodePlace.None : new NodePlace.Listed(found);
    }

    private static async Task<NodePlace> PlaceParentAsync(BrowseSession session, LoadedModule module, MetadataModule main, EntityHandle handle,
        CancellationToken ct)
    {
        if (handle.Kind != HandleKind.TypeDefinition)
        {
            return await PlaceOwnerAsync(session, module, main, handle, ct);
        }

        var parent = ReadTypeParent(module, (TypeDefinitionHandle)handle);
        if (parent is not Lookup<TypeParent>.Found read)
        {
            return parent is Lookup<TypeParent>.Unreadable unreadable ? new NodePlace.Unreadable(unreadable.Module) : NodePlace.None;
        }

        // Chosen after the read, on what it read: a nested type sits under its owner, any other under its namespace.
        if (read.Value.Namespace is null)
        {
            return await PlaceOwnerAsync(session, module, main, handle, ct);
        }

        return await PlaceNamespaceAsync(session, module, read.Value.Namespace, ct);
    }

    // The library's read of the type's row, under its own catch: the namespace of a type nested in none, null for a
    // nested one; the module unreadable, with why, when the library failed reading it (the user's data, said as the
    // module's).
    private static Lookup<TypeParent> ReadTypeParent(LoadedModule module, TypeDefinitionHandle handle)
    {
        try
        {
            var metadata = LoadedAssemblies.GetFile(module).Metadata;
            var type = metadata.GetTypeDefinition(handle);
            var declaringType = type.GetDeclaringType();
            var ns = declaringType.IsNil ? metadata.GetString(type.Namespace) : null;
            return new Lookup<TypeParent>.Found { Value = new TypeParent(ns) };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Lookup<TypeParent>.Unreadable { Module = new UnreadableModule { Module = module, Why = e } };
        }
    }

    // What holds a type: its namespace, or null for a type nested in another.
    private sealed record TypeParent(string? Namespace);

    // The library's read of the entity holding handle (an accessor's property or event, a member's or nested type's
    // type), under its own catch: none when nothing holds it; the module unreadable, with why, when the library failed
    // reading it.
    private static Lookup<EntityHandle> ReadOwner(LoadedModule module, MetadataModule main, EntityHandle handle)
    {
        try
        {
            var owner = LoadedAssemblies.FindOwner(main, handle);
            return owner is null ? new Lookup<EntityHandle>.NotFound() : new Lookup<EntityHandle>.Found { Value = owner.Value };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Lookup<EntityHandle>.Unreadable { Module = new UnreadableModule { Module = module, Why = e } };
        }
    }

    /// <summary>What a lookup of the one target <paramref name="handle"/> answers: its node, or the node of the nearest
    /// entity holding it that the tree lists, as a compiler-generated member is held by the type it is generated in
    /// (an ancestor stands in unsaid only for what the tree doesn't list by design); the failure, with why, of the
    /// children that would list it or an entity on the way, never passed over for an ancestor nor not found; the module
    /// unreadable, with why, when the library failed reading its metadata on the way; not found when nothing on the way
    /// is listed. <paramref name="main"/> is the module's type system as read.</summary>
    internal static async Task<Outcome<Lookup<BrowseNode>>> FindOrAncestorAsync(BrowseSession session, LoadedModule module, MetadataModule main,
        EntityHandle handle, CancellationToken ct)
    {
        var place = await PlaceOrAncestorAsync(session, module, main, handle, ct);
        return place.ToLookup();
    }

    /// <summary>Where <paramref name="handle"/> sits in the tree, or the nearest entity holding it that the tree lists,
    /// as a compiler-generated member is held by the type it is generated in; a failure on the way is the answer, never
    /// passed over for an ancestor. <paramref name="main"/> is the module's type system as read.</summary>
    internal static async Task<NodePlace> PlaceOrAncestorAsync(BrowseSession session, LoadedModule module, MetadataModule main, EntityHandle handle,
        CancellationToken ct)
    {
        var current = handle;
        while (true)
        {
            var place = await PlaceNodeAsync(session, module, main, current, ct);
            if (place is not NodePlace.NotListed)
            {
                return place;
            }

            var owner = ReadOwner(module, main, current);
            if (owner is not Lookup<EntityHandle>.Found found)
            {
                return owner is Lookup<EntityHandle>.Unreadable unreadable ? new NodePlace.Unreadable(unreadable.Module) : NodePlace.None;
            }

            current = found.Value;
        }
    }

    private static async Task<NodePlace> PlaceOwnerAsync(BrowseSession session, LoadedModule module, MetadataModule main, EntityHandle handle,
        CancellationToken ct)
    {
        var owner = ReadOwner(module, main, handle);
        if (owner is not Lookup<EntityHandle>.Found found)
        {
            return owner is Lookup<EntityHandle>.Unreadable unreadable ? new NodePlace.Unreadable(unreadable.Module) : NodePlace.None;
        }

        return await PlaceNodeAsync(session, module, main, found.Value, ct);
    }

    private static async Task<NodePlace> PlaceNamespaceAsync(BrowseSession session, LoadedModule module, string name, CancellationToken ct)
    {
        var namespaces = await GetChildrenAsync(session, await EnsureRootAsync(session, module, ct), ct);
        if (namespaces is ChildrenState.Failed failed)
        {
            // The module's namespaces failed to build: none is listed.
            return new NodePlace.Failed(failed.Error);
        }

        var found = session.FindNamespace(module, name);
        return found is null ? NodePlace.None : new NodePlace.Listed(found);
    }

    /// <summary>The node of the namespace, type or member <paramref name="idString"/> names in
    /// <paramref name="module"/>, whose type system read is <paramref name="main"/>'s, written as XML documentation ids
    /// are (<c>N:</c>, <c>T:</c>, <c>M:</c>, <c>P:</c>, <c>F:</c> or <c>E:</c> and the full name); not found when it
    /// defines none such, or the id cannot be read; the failure, with why, of the children that would list it, as
    /// <see cref="FindOrAncestorAsync"/> answers it; the module unreadable, with why, when the library failed reading
    /// it looking for the id.</summary>
    internal static async Task<Outcome<Lookup<BrowseNode>>> FindByIdAsync(BrowseSession session, LoadedModule module, MetadataModule main,
        string idString, CancellationToken ct)
    {
        if (idString.StartsWith(NamespaceIdPrefix, StringComparison.Ordinal))
        {
            var place = await PlaceNamespaceAsync(session, module, idString.Substring(NamespaceIdPrefix.Length), ct);
            return place.ToLookup();
        }

        var entity = ReadDefinedEntity(module, main, idString);
        if (entity is not Lookup<EntityHandle>.Found found)
        {
            return new Outcome<Lookup<BrowseNode>>.Ok(entity is Lookup<EntityHandle>.Unreadable unreadable
                ? new Lookup<BrowseNode>.Unreadable { Module = unreadable.Module }
                : new Lookup<BrowseNode>.NotFound());
        }

        return await FindOrAncestorAsync(session, module, main, found.Value, ct);
    }

    // The library's read of the entity idString names that the module defines, under its own catch: its handle, none
    // when it defines none such; the module unreadable, with why, when the library failed reading it (a member's name
    // it can't read among those it compares).
    private static Lookup<EntityHandle> ReadDefinedEntity(LoadedModule module, MetadataModule main, string idString)
    {
        try
        {
            var entity = LoadedAssemblies.FindDefinedEntity(main, idString);
            return entity is null ? new Lookup<EntityHandle>.NotFound() : new Lookup<EntityHandle>.Found { Value = entity.MetadataToken };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Lookup<EntityHandle>.Unreadable { Module = new UnreadableModule { Module = module, Why = e } };
        }
    }

    /// <summary>The row <paramref name="handle"/> names in <paramref name="module"/>'s own metadata
    /// tables; not found when it names no table row; unreadable when the library could not read the module's type
    /// system, which listing the module's folders reads; the failure, with why, of the folders or tables on the way
    /// whose children failed to build, never not found for the row they hold. The module is added to
    /// <paramref name="reading"/>, the unit's: not found when it was released (closed) before.</summary>
    internal static async Task<Outcome<Lookup<MetadataTarget>>> FindMetadataRowAsync(BrowseSession session, ModuleReadLease reading,
        LoadedModule module, EntityHandle handle, CancellationToken ct)
    {
        if (!MetadataTokens.TryGetTableIndex(handle.Kind, out var index) || !LoadedAssemblies.TryRead(reading, module))
        {
            return new Outcome<Lookup<MetadataTarget>>.Ok(new Lookup<MetadataTarget>.NotFound());
        }

        var folders = await ReadChildrenAsync(session, await EnsureRootAsync(session, module, ct), ct);
        var metadata = FindAmongReadable(folders, LoadedAssemblies.GetFile(module), static (listed, file) => listed.FirstOrDefault(f => IsMetadataOf(f, file)));
        var tables = await ThenFindChildAsync(session, metadata, 0, static (listed, _) => listed.FirstOrDefault(static c => c.Subject is NodeSubject.MetadataTables), ct);
        var table = await ThenFindChildAsync(session, tables, index,
            static (listed, i) => listed.FirstOrDefault(c => c.Subject is NodeSubject.MetadataTable part && part.Value.ListedTable == i), ct);
        var row = MetadataTokens.GetRowNumber(handle) - 1;
        return table switch
        {
            Outcome<Lookup<BrowseNode>>.Ok ok => new Outcome<Lookup<MetadataTarget>>.Ok(ok.Value.Select(node => new MetadataTarget { Node = node, Row = row })),
            Outcome<Lookup<BrowseNode>>.Failed failed => new Outcome<Lookup<MetadataTarget>>.Failed(failed.Error),
        };
    }

    // The child pick picks among the children of the node reached; what was reached otherwise, as it was.
    private static async Task<Outcome<Lookup<BrowseNode>>> ThenFindChildAsync<TState>(BrowseSession session, Outcome<Lookup<BrowseNode>> reached,
        TState state, Func<IReadOnlyList<BrowseNode>, TState, BrowseNode?> pick, CancellationToken ct)
    {
        if (reached is not Outcome<Lookup<BrowseNode>>.Ok { Value: Lookup<BrowseNode>.Found node })
        {
            return reached;
        }

        return FindAmong(await GetChildrenAsync(session, node.Value, ct), state, pick);
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
}
