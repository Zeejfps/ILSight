using System.Diagnostics;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using AssemblyReference = ICSharpCode.Decompiler.Metadata.AssemblyReference;

namespace ILSight.Core;

/// <summary>Opening, reading, listing and closing what an <see cref="AssemblyList"/> holds, and finding what its
/// assemblies refer to. All of this is thread-safe: it reads a file, then hands what it read to the list, which
/// admits, lists or drops it holding its own lock.</summary>
public static class AssemblyListing
{
    /// <summary>Opens the assembly at <paramref name="path"/>, or returns it if already open. Its type
    /// system starts loading in the background. A file that is missing or not a .NET assembly is listed
    /// all the same, as a <see cref="FailedAssembly"/>, and the assembly takes its place
    /// once the file loads. Once <paramref name="cancellationToken"/> is cancelled
    /// the assembly is no longer listed, so cancelling before <see cref="AssemblyList.CloseAll"/> keeps the opens of
    /// several files out of the list that follows.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled,
    /// or the list was cleared while the file was read.</exception>
    public static LoadedAssembly Open(AssemblyList list, AssemblyPath path, CancellationToken cancellationToken = default)
    {
        var generation = list.Generation;
        while (true)
        {
            var assembly = GetAssemblyOrFailure(list, path, generation);
            if (TryList(list, assembly, generation, cancellationToken))
            {
                return assembly;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (generation != list.Generation)
            {
                throw new OperationCanceledException($"The list was cleared while '{path}' was opened.");
            }
        }
    }

    private static LoadedAssembly GetAssemblyOrFailure(AssemblyList list, AssemblyPath path, int generation)
    {
        try
        {
            return GetAssembly(list, path, generation);
        }
        catch (AssemblyLoadException e)
        {
            return list.FindListedFailure(path) ?? LoadedAssemblies.CreateFailed(list, e);
        }
    }

    /// <summary>Takes from <paramref name="options"/> the toggles that apply as assemblies are read: whether
    /// references load their assemblies, and whether Windows Runtime metadata is projected. Returns true
    /// when they changed, so that the open assemblies must be read again to follow them. A change ends the
    /// <see cref="AssemblyList.Generation"/>, as a clear does, so that no assembly read with the options before is kept.</summary>
    public static bool TryChangeLoadOptions(AssemblyList list, DecompilerOptions options)
    {
        var loadsReferences = DecompilerToggles.IsOn(options, DecompilerToggle.AutoLoadAssemblyReferences);
        var readerOptions = ChooseReaderOptions(options);
        if (AssemblyList.IsSame(list.Now.ReadOptions, loadsReferences, readerOptions))
        {
            return false;
        }

        return list.TryChangeReadOptions(loadsReferences, readerOptions);
    }

    private static MetadataReaderOptions ChooseReaderOptions(DecompilerOptions options)
    {
        if (DecompilerToggles.IsOn(options, DecompilerToggle.ApplyWindowsRuntimeProjections))
        {
            return MetadataReaderOptions.ApplyWindowsRuntimeProjections;
        }

        return MetadataReaderOptions.None;
    }

    /// <summary>Reads the item of the list holding <paramref name="assembly"/> again from its file: the
    /// assembly, or the package it was read out of with the assemblies in it, each taking what is picked
    /// for it now, handed back with them. It is not listed until <see cref="ClosableAssemblyList.TryReplace"/> puts
    /// it in the item's place, which reads nothing more. Reads the file: call it off the UI thread.</summary>
    /// <exception cref="AssemblyLoadException">The file can no longer be read.</exception>
    public static FreshItem ReadAgain(AssemblyList list, LoadedAssembly assembly)
    {
        var fresh = ReadAssemblyOrPackage(list, assembly.ListedItem.FilePath);
        if (fresh is LoadedPackage package)
        {
            return new FreshItem { Item = fresh, ReadOutOf = LoadedAssemblies.EnsureAssemblies(package) };
        }

        return new FreshItem { Item = fresh, ReadOutOf = [] };
    }

    /// <summary>Lists the assemblies <paramref name="assembly"/> references that can be found, as
    /// following a reference to them does, and returns them. Those that cannot be found are left out; none when
    /// <paramref name="assembly"/> was released (closed) before. May load files: call it off the UI thread.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static List<LoadedModule> ListReferences(AssemblyList list, LoadedModule assembly, CancellationToken cancellationToken = default)
    {
        var generation = list.Generation;
        var listed = new List<LoadedModule>();
        using var reading = LoadedAssemblies.BeginReading();
        if (!LoadedAssemblies.TryRead(reading, assembly))
        {
            return listed;
        }

        foreach (var reference in LoadedAssemblies.GetFile(assembly).AssemblyReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = LoadedAssemblies.GetResolver(assembly).FindAssembly(reference);
            if (found is null)
            {
                continue;
            }

            if (TryList(list, found, generation, cancellationToken))
            {
                listed.Add(found);
            }
        }
        return listed;
    }
    /// <summary>The listed assembly that defines what <paramref name="reference"/> points at, and the
    /// handle of its definition there: the assembly's own handle for an assembly. Not found when it cannot be resolved
    /// or listed; unreadable when resolving it reads the type system of a module the library could not read. Each module
    /// it reads is added to <paramref name="reading"/>, the unit's: one released meanwhile is passed over, as one no
    /// longer listed.</summary>
    internal static async Task<Lookup<(LoadedModule Assembly, EntityHandle Handle)>> ResolveListedAsync(AssemblyList list, ModuleReading reading,
        CodeReference.Navigable reference, CancellationToken ct)
    {
        var generation = list.Generation;
        var owner = TryLoadAssembly(list, reference.FilePath, generation);
        if (owner is null || !LoadedAssemblies.TryRead(reading, owner))
        {
            return new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.NotFound();
        }

        var resolved = await ResolveTargetAsync(list, reading, owner, reference, generation, ct);
        return resolved.Match<Lookup<(LoadedModule Assembly, EntityHandle Handle)>>(
            found => ListResolved(list, found, generation, ct),
            static notFound => notFound,
            static unreadable => unreadable);
    }

    private static Lookup<(LoadedModule Assembly, EntityHandle Handle)> ListResolved(AssemblyList list,
        Lookup<(LoadedModule Assembly, EntityHandle Handle)>.Found resolved, int generation, CancellationToken ct)
    {
        if (!TryList(list, resolved.Value.Assembly, generation, ct))
        {
            return new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.NotFound();
        }

        return resolved;
    }

    private static async Task<Lookup<(LoadedModule Assembly, EntityHandle Handle)>> ResolveTargetAsync(AssemblyList list, ModuleReading reading,
        LoadedModule owner, CodeReference.Navigable reference, int generation, CancellationToken ct)
    {
        switch (reference)
        {
            case CodeReference.Entity entity:
                return await ResolveDefinitionAsync(list, reading, owner, entity.Handle, generation, ct);
            case CodeReference.Assembly assembly:
                if (assembly.Referenced is null)
                {
                    return Found(owner, EntityHandle.AssemblyDefinition);
                }

                var target = LoadedAssemblies.GetResolver(owner).ResolveAssembly(new AssemblyReference(LoadedAssemblies.GetFile(owner), assembly.Referenced.Value));
                if (target is null)
                {
                    return new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.NotFound();
                }

                return Found(target, EntityHandle.AssemblyDefinition);
            default:
                throw new UnreachableException();
        }
    }

    private static Lookup<(LoadedModule Assembly, EntityHandle Handle)> Found(LoadedModule assembly, EntityHandle handle)
    {
        return new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.Found { Value = (assembly, handle) };
    }

    /// <summary>
    /// What hovering <paramref name="reference"/> explains: the declaration and XML documentation of
    /// the type or member it points at. Not found when it cannot be resolved; unreadable when the library could not
    /// read the type system of an assembly resolving it reads. Loads the files it needs without listing them, and
    /// reads documentation files: call it off the UI thread.
    /// </summary>
    public static async Task<Lookup<CodeTooltip>> DescribeReferenceAsync(AssemblyList list, CodeReference.Entity reference, CancellationToken ct)
    {
        try
        {
            using var reading = LoadedAssemblies.BeginReading();
            var resolved = await ResolveEntityAsync(list, reading, reference, ct);
            return resolved.Select(static r => Documentation.CreateTooltip(r.Entity, r.Assembly, r.TypeSystem));
        }
        catch (BadImageFormatException)
        {
            return new Lookup<CodeTooltip>.NotFound();
        }
        catch (IOException)
        {
            return new Lookup<CodeTooltip>.NotFound();
        }
    }

    /// <summary>
    /// The page of the .NET API reference about the type or member <paramref name="reference"/>
    /// points at, as <see cref="MicrosoftDocs.FindPage(BrowseNode)"/> names it. Not found for what is not
    /// visible outside its assembly, or when it cannot be resolved; unreadable as
    /// <see cref="DescribeReferenceAsync"/> says. Loads the files it needs without listing them: call it off the UI
    /// thread.
    /// </summary>
    public static async Task<Lookup<string>> FindApiReferencePageAsync(AssemblyList list, CodeReference.Entity reference, CancellationToken ct)
    {
        try
        {
            using var reading = LoadedAssemblies.BeginReading();
            var resolved = await ResolveEntityAsync(list, reading, reference, ct);
            return resolved.Match(
                static found => Lookup.FromNullable(MicrosoftDocs.FindPage(found.Value.Entity)),
                static _ => new Lookup<string>.NotFound(),
                static unreadable => new Lookup<string>.Unreadable { Module = unreadable.Module });
        }
        catch (BadImageFormatException)
        {
            return new Lookup<string>.NotFound();
        }
        catch (IOException)
        {
            return new Lookup<string>.NotFound();
        }
    }

    /// <summary>The definition of the type <paramref name="reference"/> points at, loading the assembly
    /// that defines it without listing it; not found when it points at no type, or cannot be resolved; unreadable as
    /// <see cref="DescribeReferenceAsync"/> says; each module read is added to <paramref name="reading"/>, the unit's,
    /// one released meanwhile passed over. Call it off the UI thread.</summary>
    internal static async Task<Lookup<ITypeDefinition>> FindTypeDefinitionAsync(AssemblyList list, ModuleReading reading, CodeReference.Entity reference,
        CancellationToken ct)
    {
        if (reference.Kind != EntityKind.Type)
        {
            return new Lookup<ITypeDefinition>.NotFound();
        }

        try
        {
            var resolved = await ResolveEntityAsync(list, reading, reference, ct);
            return resolved.Match(
                static found => Lookup.FromNullable(found.Value.Entity as ITypeDefinition),
                static _ => new Lookup<ITypeDefinition>.NotFound(),
                static unreadable => new Lookup<ITypeDefinition>.Unreadable { Module = unreadable.Module });
        }
        catch (BadImageFormatException)
        {
            return new Lookup<ITypeDefinition>.NotFound();
        }
        catch (IOException)
        {
            return new Lookup<ITypeDefinition>.NotFound();
        }
    }

    /// <summary>The definition of the type <paramref name="anchor"/> names, loading the assembly that
    /// defines it without listing it; not found when its file cannot be read or no longer defines it; unreadable when
    /// the library could not read its type system. Call it off the UI thread.</summary>
    internal static async Task<Lookup<ITypeDefinition>> FindTypeDefinitionAsync(AssemblyList list, ModuleReading reading, TypeAnchor anchor,
        CancellationToken ct)
    {
        try
        {
            var assembly = TryLoadAssembly(list, anchor.AssemblyPath);
            if (assembly is null)
            {
                return new Lookup<ITypeDefinition>.NotFound();
            }

            var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
            return read.Match(
                r => Lookup.FromNullable(r.TypeSystem.MainModule.GetTypeDefinition(new FullTypeName(anchor.FullName))),
                u => Lookup.FromUnreadable<ITypeDefinition>(assembly, u),
                static _ => new Lookup<ITypeDefinition>.NotFound());
        }
        catch (BadImageFormatException)
        {
            return new Lookup<ITypeDefinition>.NotFound();
        }
        catch (IOException)
        {
            return new Lookup<ITypeDefinition>.NotFound();
        }
    }

    private static async Task<Lookup<ResolvedEntity>> ResolveEntityAsync(AssemblyList list, ModuleReading reading, CodeReference.Entity entity,
        CancellationToken ct)
    {
        var generation = list.Generation;
        var owner = TryLoadAssembly(list, entity.FilePath, generation);
        if (owner is null)
        {
            return new Lookup<ResolvedEntity>.NotFound();
        }

        var definition = await ResolveDefinitionAsync(list, reading, owner, entity.Handle, generation, ct);
        return await definition.ThenFindAsync((found, c) => ResolveEntityAsync(reading, found.Assembly, found.Handle, c), ct);
    }

    private static async Task<Lookup<ResolvedEntity>> ResolveEntityAsync(ModuleReading reading, LoadedModule assembly, EntityHandle handle,
        CancellationToken ct)
    {
        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, assembly, ct);
        return read.Match(
            r => Lookup.FromNullable(Resolve(r.TypeSystem, assembly, handle)),
            u => Lookup.FromUnreadable<ResolvedEntity>(assembly, u),
            static _ => new Lookup<ResolvedEntity>.NotFound());
    }

    private sealed class ResolvedEntity
    {
        public required IEntity Entity { get; init; }

        public required LoadedModule Assembly { get; init; }

        public required DecompilerTypeSystem TypeSystem { get; init; }
    }

    private static ResolvedEntity? Resolve(DecompilerTypeSystem typeSystem, LoadedModule assembly, EntityHandle handle)
    {
        var resolved = typeSystem.MainModule.ResolveEntity(handle);
        if (resolved is null)
        {
            return null;
        }

        return new ResolvedEntity { Entity = resolved, Assembly = assembly, TypeSystem = typeSystem };
    }

    /// <summary>The listed item <paramref name="path"/> names: its assembly, opened and listed if it is not
    /// and its file still exists, or the file listed as failed there. Null if the file cannot be opened,
    /// or the list was cleared meanwhile. May load files: call it off the UI thread.</summary>
    internal static LoadedAssembly? OpenListed(AssemblyList list, AssemblyPath path)
    {
        var generation = list.Generation;
        var assembly = TryLoad(list, path, generation) ?? list.FindListedFailure(path);
        if (assembly is null || !TryList(list, assembly, generation))
        {
            return null;
        }

        return assembly;
    }

    /// <summary>The item the list holds now that was read from <paramref name="path"/>; null when it
    /// holds none there, or only a file that failed.</summary>
    internal static LoadedAssembly? FindListedLoaded(AssemblyList list, AssemblyPath path)
    {
        var listed = list.Now.Listed;
        foreach (var assembly in listed)
        {
            if (assembly is FailedAssembly)
            {
                continue;
            }

            if (assembly.FilePath.Equals(path))
            {
                return assembly;
            }
        }
        return null;
    }

    internal static LoadedAssembly? TryLoad(AssemblyList list, AssemblyPath path)
    {
        return TryLoad(list, path, list.Generation);
    }

    /// <summary>The assembly at <paramref name="path"/>, as <see cref="TryLoad(AssemblyList, AssemblyPath, int)"/> loads it;
    /// null for a package, which has no metadata of its own, or a file that failed to load.</summary>
    internal static LoadedModule? TryLoadAssembly(AssemblyList list, AssemblyPath path)
    {
        return TryLoadAssembly(list, path, list.Generation);
    }

    private static LoadedModule? TryLoadAssembly(AssemblyList list, AssemblyPath path, int generation)
    {
        var assembly = TryLoad(list, path, generation);
        return assembly is LoadedModule module ? module : null;
    }

    internal static LoadedAssembly? TryLoad(AssemblyList list, AssemblyPath path, int generation)
    {
        try
        {
            return GetAssembly(list, path, generation);
        }
        catch (AssemblyLoadException)
        {
            return null;
        }
    }

    private static LoadedAssembly GetAssembly(AssemblyList list, AssemblyPath path, int generation)
    {
        var existing = list.FindRead(path);
        if (existing is not null)
        {
            return existing;
        }

        var read = Read(list, path, generation);
        var admitted = list.Admit(path, read, generation);
        ReleaseUnkept(admitted.Unkept);
        return admitted.Use;
    }

    internal static bool TryList(AssemblyList list, LoadedAssembly assembly)
    {
        return TryList(list, assembly, list.Generation);
    }

    /// <summary>Lists <paramref name="assembly"/>, or for one read out of a package the package, unless
    /// the list was cleared since <paramref name="generation"/>, <paramref name="cancellationToken"/>
    /// was cancelled, or another assembly took its path. A package's assemblies are read before it is
    /// listed, so that listing never reads files while holding the lock.</summary>
    internal static bool TryList(AssemblyList list, LoadedAssembly assembly, int generation, CancellationToken cancellationToken = default)
    {
        if (assembly is LoadedModule module && module.Place is not null)
        {
            return TryList(list, module.Place.Container, generation, cancellationToken);
        }

        var read = new FreshItem { Item = assembly, ReadOutOf = EnsureReadOutOf(assembly) };
        return list.TryList(read, generation, cancellationToken);
    }

    private static IReadOnlyList<LoadedModule> EnsureReadOutOf(LoadedAssembly assembly)
    {
        if (assembly is LoadedPackage package)
        {
            return LoadedAssemblies.EnsureAssemblies(package);
        }

        return [];
    }

    /// <summary>The module of the list read from <paramref name="module"/>; throws for one it did not read.</summary>
    internal static LoadedModule GetLoadedFrom(AssemblyList list, MetadataFile module)
    {
        foreach (var assembly in list.ListRead())
        {
            if (LoadedAssemblies.IsReadFrom(assembly, module))
            {
                return assembly;
            }
        }
        throw new InvalidOperationException($"{module.FileName} was not loaded by list list.");
    }

    /// <summary>The assembly <paramref name="package"/> holds as <paramref name="file"/>: the one read
    /// before, else one read now, kept while the list holds the package. Null when it does not load.</summary>
    internal static LoadedModule? TryLoadOutOf(AssemblyList list, LoadedPackage package, PackageFile file)
    {
        var path = AssemblyPackages.GetFilePath(package.Package, file);
        var known = list.FindReadOutOf(package, path);
        if (known is not null)
        {
            return known;
        }

        var read = TryReadOutOfPackage(list, package, path, file.FullName);
        if (read is null)
        {
            return null;
        }

        var admitted = list.AdmitReadOutOf(package, path, read);
        ReleaseUnkept(admitted.Unkept);
        return admitted.Use;
    }

    // An unkept read (the list was cleared meanwhile, or another read of its file was kept first) is on no list, so
    // its library's objects go once whoever was handed it stops reading it. One read out of a package the list doesn't
    // hold, a package read again among them, is its package's, released with it.
    private static void ReleaseUnkept(LoadedAssembly? assembly)
    {
        if (assembly is not LoadedModule module)
        {
            return;
        }

        LoadedAssemblies.Release(new RemovedModules { Modules = [module] });
    }

    private static LoadedModule? TryReadOutOfPackage(AssemblyList list, LoadedPackage package, AssemblyPath path, string fullName)
    {
        try
        {
            return ReadOutOfPackage(list, package, path, fullName);
        }
        catch (AssemblyLoadException)
        {
            return null;
        }
    }

    private static async Task<Lookup<(LoadedModule Assembly, EntityHandle Handle)>> ResolveDefinitionAsync(AssemblyList list, ModuleReading reading,
        LoadedModule owner, EntityHandle handle, int generation, CancellationToken ct)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition or HandleKind.MethodDefinition or HandleKind.FieldDefinition
                or HandleKind.PropertyDefinition or HandleKind.EventDefinition:
                return Found(owner, handle);
        }

        var read = await LoadedAssemblies.ReadTypeSystemAsync(reading, owner, ct);
        return read.Match(
            r => ResolveDefinition(list, r.TypeSystem.MainModule, handle, generation),
            u => Lookup.FromUnreadable<(LoadedModule Assembly, EntityHandle Handle)>(owner, u),
            static _ => new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.NotFound());
    }

    private static Lookup<(LoadedModule Assembly, EntityHandle Handle)> ResolveDefinition(AssemblyList list, MetadataModule main, EntityHandle handle,
        int generation)
    {
        var definition = Definitions.Resolve(main, handle);
        if (definition is null)
        {
            return new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.NotFound();
        }

        var assembly = TryLoadAssembly(list, AssemblyPath.Of(DecompilerFacts.GetMetadataFile(definition).FileName), generation);
        if (assembly is null)
        {
            return new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.NotFound();
        }

        return Found(assembly, definition.MetadataToken);
    }

    private static LoadedAssembly Read(AssemblyList list, AssemblyPath path, int generation)
    {
        if (File.Exists(path.Text))
        {
            return ReadAssemblyOrPackage(list, path);
        }

        // A path that names no file may name an assembly in a package: AssemblyPackages.GetFilePath made it.
        var inPackage = AssemblyPackages.SplitPath(path);
        if (inPackage is null)
        {
            return LoadedAssemblies.CreateModule(list, path, ReadFile(list, path));
        }

        var container = GetAssembly(list, inPackage.Value.Package, generation);
        if (container is not LoadedPackage package)
        {
            throw new AssemblyLoadException(path, $"'{container.FilePath}' is not a package.");
        }

        return ReadOutOfPackage(list, package, path, inPackage.Value.FullName);
    }

    private static LoadedAssembly ReadAssemblyOrPackage(AssemblyList list, AssemblyPath path)
    {
        try
        {
            return LoadedAssemblies.CreateModule(list, path, ReadFile(list, path));
        }
        catch (AssemblyLoadException)
        {
            var read = ReadWebCilOrPackage(list, path);
            if (read is null)
            {
                throw;
            }

            return read;
        }
    }

    private static LoadedAssembly? ReadWebCilOrPackage(AssemblyList list, AssemblyPath path)
    {
        var module = ReadWebCil(list, path);
        if (module is not null)
        {
            return LoadedAssemblies.CreateModule(list, path, module);
        }

        var package = ReadPackage(path);
        if (package is null)
        {
            return null;
        }

        return LoadedAssemblies.CreatePackage(list, package);
    }

    // WebCIL is an assembly wrapped in WebAssembly, the way Blazor ships it.
    private static WebCilFile? ReadWebCil(AssemblyList list, AssemblyPath path)
    {
        try
        {
            return WebCilFile.FromFile(path.Text, list.Now.ReadOptions.ReaderOptions);
        }
        catch (IOException e)
        {
            throw CannotOpen(path, e);
        }
        catch (UnauthorizedAccessException e)
        {
            throw CannotOpen(path, e);
        }
        catch (BadImageFormatException e)
        {
            throw CannotOpen(path, e);
        }
        catch (InvalidOperationException e)
        {
            throw CannotOpen(path, e);
        }
    }

    private static AssemblyPackage? ReadPackage(AssemblyPath path)
    {
        try
        {
            return AssemblyPackages.TryRead(path);
        }
        catch (IOException e)
        {
            throw CannotOpen(path, e);
        }
        catch (UnauthorizedAccessException e)
        {
            throw CannotOpen(path, e);
        }
    }

    private static LoadedModule ReadOutOfPackage(AssemblyList list, LoadedPackage container, AssemblyPath path, string fullName)
    {
        var package = container.Package;
        var file = AssemblyPackages.FindFile(package, fullName);
        if (file is null)
        {
            throw new AssemblyLoadException(path, $"'{container.FilePath}' holds no '{fullName}'.");
        }

        var image = ReadPackageFile(list, package, file);
        return LoadedAssemblies.CreateModule(list, path, image, new PackagePlace
        {
            Container = container,
            File = file,
        });
    }

    private static PEFile ReadPackageFile(AssemblyList list, AssemblyPackage package, PackageFile file)
    {
        var path = AssemblyPackages.GetFilePath(package, file);
        try
        {
            var bytes = AssemblyPackages.ReadFile(package, file);
            using var stream = new MemoryStream(bytes);
            return ReadImage(list, path, stream);
        }
        catch (IOException e)
        {
            throw CannotOpen(path, e);
        }
        catch (InvalidDataException e)
        {
            throw CannotOpen(path, e);
        }
        catch (UnauthorizedAccessException e)
        {
            throw CannotOpen(path, e);
        }
    }

    private static PEFile ReadFile(AssemblyList list, AssemblyPath path)
    {
        try
        {
            using var stream = new FileStream(path.Text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return ReadImage(list, path, stream);
        }
        catch (IOException e)
        {
            throw CannotOpen(path, e);
        }
        catch (UnauthorizedAccessException e)
        {
            throw CannotOpen(path, e);
        }
    }

    private static PEFile ReadImage(AssemblyList list, AssemblyPath path, Stream stream)
    {
        try
        {
            var file = new PEFile(path.Text, stream, PEStreamOptions.PrefetchEntireImage, list.Now.ReadOptions.ReaderOptions);
            if (!file.Reader.HasMetadata)
            {
                throw new AssemblyLoadException(path, $"'{path}' is not a .NET assembly.");
            }

            return file;
        }
        catch (BadImageFormatException e)
        {
            throw CannotOpen(path, e);
        }
        catch (InvalidOperationException e)
        {
            throw CannotOpen(path, e);
        }
        catch (MetadataFileNotSupportedException e)
        {
            throw CannotOpen(path, e);
        }
    }

    private static AssemblyLoadException CannotOpen(AssemblyPath path, Exception e)
    {
        return new AssemblyLoadException(path, $"Cannot open '{path}': {e.Message}", e);
    }
}
