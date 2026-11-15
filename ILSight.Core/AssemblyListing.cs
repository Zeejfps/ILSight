using System.Diagnostics;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.ExceptionServices;
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
    /// several files out of the list that follows: every change that ends the list's <see cref="AssemblyList.Generation"/>
    /// (a clear, new load options) cancels the generation's token first (<see cref="AssemblyList.GenerationEnding"/>),
    /// which the edge links into each open's token.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="InvalidOperationException">The generation ended while the file was read and
    /// <paramref name="cancellationToken"/> was not cancelled: an open whose token was not linked to the generation's,
    /// or a change that ended the generation without cancelling its work.</exception>
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
                throw new InvalidOperationException($"The list's generation {generation} ended while '{path}' was opened, with its open not cancelled: an open's token links the list's GenerationEnding, which the generation's end cancels first.");
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
    /// <see cref="AssemblyList.Generation"/>, as a clear does, its work cancelled first (<see cref="AssemblyList.GenerationEnding"/>),
    /// so that no assembly read with the options before is kept.</summary>
    public static bool TryChangeLoadOptions(AssemblyList list, DecompilerOptions options)
    {
        if (!ChangesLoadOptions(list, options))
        {
            return false;
        }

        return list.TryChangeReadOptions(DecompilerToggles.IsOn(options, DecompilerToggle.AutoLoadAssemblyReferences), ChooseReaderOptions(options));
    }

    // Whether taking the options would change the toggles that apply as assemblies are read.
    private static bool ChangesLoadOptions(AssemblyList list, DecompilerOptions options)
    {
        var loadsReferences = DecompilerToggles.IsOn(options, DecompilerToggle.AutoLoadAssemblyReferences);
        return !AssemblyList.IsSame(list.Now.ReadOptions, loadsReferences, ChooseReaderOptions(options));
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
    /// it in the item's place, which reads nothing more. A file that can no longer be read is
    /// <see cref="Outcome{T}.Failed"/> with its <see cref="AssemblyLoadException"/>. Reads the file: call it off the UI
    /// thread.</summary>
    public static Outcome<FreshItem> ReadAgain(AssemblyList list, LoadedAssembly assembly)
    {
        LoadedAssembly fresh;
        try
        {
            fresh = ReadAssemblyOrPackage(list, assembly.ListedItem.FilePath);
        }
        catch (AssemblyLoadException e)
        {
            return new Outcome<FreshItem>.Failed(e);
        }

        if (fresh is LoadedPackage package)
        {
            return new Outcome<FreshItem>.Ok(new FreshItem { Item = fresh, ReadOutOf = LoadedAssemblies.EnsureAssemblies(package) });
        }

        return new Outcome<FreshItem>.Ok(new FreshItem { Item = fresh, ReadOutOf = [] });
    }

    /// <summary>Lists the assemblies <paramref name="assembly"/> references that can be found, as
    /// following a reference to them does, and returns them. Those that cannot be found are left out; none when
    /// <paramref name="assembly"/> was released (closed) before. What the library throws reading the references or
    /// resolving one is <see cref="Outcome{T}.Failed"/>, those listed before it staying listed. May load files: call it
    /// off the UI thread.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static Outcome<List<LoadedModule>> ListReferences(AssemblyList list, LoadedModule assembly, CancellationToken cancellationToken = default)
    {
        var generation = list.Generation;
        using var reading = LoadedAssemblies.BeginReading();
        if (!LoadedAssemblies.TryRead(reading, assembly))
        {
            return new Outcome<List<LoadedModule>>.Ok([]);
        }

        List<AssemblyReference> references;
        try
        {
            references = LoadedAssemblies.GetFile(assembly).AssemblyReferences.ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<List<LoadedModule>>.Failed(e);
        }

        return ListFound(list, LoadedAssemblies.GetResolver(assembly), references, generation, cancellationToken);
    }

    private static Outcome<List<LoadedModule>> ListFound(AssemblyList list, AssemblyResolver resolver, List<AssemblyReference> references, int generation,
        CancellationToken cancellationToken)
    {
        var listed = new List<LoadedModule>();
        foreach (var reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadedModule? found;
            try
            {
                found = resolver.FindAssembly(reference);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                return new Outcome<List<LoadedModule>>.Failed(e);
            }

            if (found is null)
            {
                continue;
            }

            if (TryList(list, found, generation, cancellationToken))
            {
                listed.Add(found);
            }
        }
        return new Outcome<List<LoadedModule>>.Ok(listed);
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
        var owner = FindAssemblyToLookIn(list, reference.FilePath, generation);
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
    /// read the type system of an assembly resolving it reads; what the library throws resolving or describing it, or
    /// reading its documentation, <see cref="Outcome{T}.Failed"/>. Loads the files it needs without listing them, and
    /// reads documentation files: call it off the UI thread.
    /// </summary>
    public static async Task<Outcome<Lookup<CodeTooltip>>> DescribeReferenceAsync(AssemblyList list, CodeReference.Entity reference,
        CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        Lookup<TooltipFacts> read;
        try
        {
            var resolved = await ResolveEntityAsync(list, reading, reference, ct);
            read = resolved.Select(static r => Documentation.ReadTooltip(r.Entity, r.Assembly, r.TypeSystem));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<CodeTooltip>>.Failed(e);
        }

        // Made of what was read, outside the catch: a bug of the app's formatting is not the library's failure.
        return new Outcome<Lookup<CodeTooltip>>.Ok(read.Select(Documentation.FormatTooltip));
    }

    /// <summary>
    /// The page of the .NET API reference about the type or member <paramref name="reference"/>
    /// points at, as <see cref="MicrosoftDocs.FindPage(BrowseNode)"/> names it. Not found for what is not
    /// visible outside its assembly, or when it cannot be resolved; unreadable as
    /// <see cref="DescribeReferenceAsync"/> says; what the library throws resolving it or naming its page,
    /// <see cref="Outcome{T}.Failed"/>. Loads the files it needs without listing them: call it off the UI thread.
    /// </summary>
    public static async Task<Outcome<Lookup<string>>> FindApiReferencePageAsync(AssemblyList list, CodeReference.Entity reference, CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        Lookup<string> page;
        try
        {
            var resolved = await ResolveEntityAsync(list, reading, reference, ct);
            page = resolved.Match(
                static found => Lookup.FromNullable(MicrosoftDocs.FindPage(found.Value.Entity)),
                static _ => new Lookup<string>.NotFound(),
                static unreadable => new Lookup<string>.Unreadable { Module = unreadable.Module });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<string>>.Failed(e);
        }

        return new Outcome<Lookup<string>>.Ok(page);
    }

    /// <summary>The definition of the type <paramref name="reference"/> points at, loading the assembly
    /// that defines it without listing it; not found when it points at no type, or cannot be resolved; unreadable as
    /// <see cref="DescribeReferenceAsync"/> says; each module read is added to <paramref name="reading"/>, the unit's,
    /// one released meanwhile passed over. What else the library throws resolving it goes on to the caller, whose read
    /// catches it as its unit's failure. Call it off the UI thread.</summary>
    internal static async Task<Lookup<ITypeDefinition>> FindTypeDefinitionAsync(AssemblyList list, ModuleReading reading, CodeReference.Entity reference,
        CancellationToken ct)
    {
        if (reference.Kind != EntityKind.Type)
        {
            return new Lookup<ITypeDefinition>.NotFound();
        }

        var resolved = await ResolveEntityAsync(list, reading, reference, ct);
        return resolved.Match(
            static found => Lookup.FromNullable(found.Value.Entity as ITypeDefinition),
            static _ => new Lookup<ITypeDefinition>.NotFound(),
            static unreadable => new Lookup<ITypeDefinition>.Unreadable { Module = unreadable.Module });
    }

    /// <summary>The definition of the type <paramref name="anchor"/> names, loading the assembly that
    /// defines it without listing it; not found when no file is there or it no longer defines it (a file there that
    /// can't be read throws its <see cref="AssemblyLoadException"/>, the lookup's failure with why); unreadable when
    /// the library could not read its type system. What else the library throws reading it goes on to the caller, as
    /// above. Call it off the UI thread.</summary>
    internal static async Task<Lookup<ITypeDefinition>> FindTypeDefinitionAsync(AssemblyList list, ModuleReading reading, TypeAnchor anchor,
        CancellationToken ct)
    {
        var assembly = FindAssemblyToLookIn(list, anchor.AssemblyPath);
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

    private static async Task<Lookup<ResolvedEntity>> ResolveEntityAsync(AssemblyList list, ModuleReading reading, CodeReference.Entity entity,
        CancellationToken ct)
    {
        var generation = list.Generation;
        var owner = FindAssemblyToLookIn(list, entity.FilePath, generation);
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
    /// and its file still exists, or the file listed as failed there. Null if no file is there, or the list was cleared
    /// meanwhile; a file there that can't be read, and isn't listed as failed, throws its
    /// <see cref="AssemblyLoadException"/>. May load files: call it off the UI thread.</summary>
    internal static LoadedAssembly? OpenListed(AssemblyList list, AssemblyPath path)
    {
        var generation = list.Generation;
        var load = TryLoad(list, path, generation);
        var assembly = load is Outcome<Lookup<LoadedAssembly>>.Ok { Value: Lookup<LoadedAssembly>.Found found } ? found.Value : list.FindListedFailure(path);
        if (assembly is null && load is Outcome<Lookup<LoadedAssembly>>.Failed failed)
        {
            ExceptionDispatchInfo.Throw(failed.Error);
        }

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

    /// <summary>The assembly at <paramref name="path"/>, loaded without listing it, as of the list's generation now
    /// (<see cref="TryLoad(AssemblyList, AssemblyPath, int)"/>).</summary>
    internal static Outcome<Lookup<LoadedAssembly>> TryLoad(AssemblyList list, AssemblyPath path)
    {
        return TryLoad(list, path, list.Generation);
    }

    /// <summary>The assembly at <paramref name="path"/> to look a saved place up in, loaded without listing it: null when
    /// no file is there (<see cref="TryLoad(AssemblyList, AssemblyPath, int)"/>), or it is a package, which has no
    /// metadata of its own. A file there that can't be read throws its <see cref="AssemblyLoadException"/>: a file that
    /// can't be read is not a file that is gone.</summary>
    internal static LoadedModule? FindAssemblyToLookIn(AssemblyList list, AssemblyPath path)
    {
        return FindAssemblyToLookIn(list, path, list.Generation);
    }

    private static LoadedModule? FindAssemblyToLookIn(AssemblyList list, AssemblyPath path, int generation)
    {
        LoadedAssembly assembly;
        try
        {
            assembly = GetAssembly(list, path, generation);
        }
        catch (AssemblyLoadException e) when (IsNotThere(e, path))
        {
            return null;
        }

        return assembly is LoadedModule module ? module : null;
    }

    /// <summary>The assembly at <paramref name="path"/>, loaded without listing it: found; not found when no file is
    /// there, at the check or by the read's <see cref="FileNotFoundException"/> or <see cref="DirectoryNotFoundException"/>
    /// naming the path asked for or its package (a file can go between the check and the read); failed, with its
    /// <see cref="AssemblyLoadException"/>, for a file there that can't be read: a lock, a corrupt file, another file it
    /// needs missing.</summary>
    internal static Outcome<Lookup<LoadedAssembly>> TryLoad(AssemblyList list, AssemblyPath path, int generation)
    {
        LoadedAssembly assembly;
        try
        {
            assembly = GetAssembly(list, path, generation);
        }
        catch (AssemblyLoadException e) when (IsNotThere(e, path))
        {
            return new Outcome<Lookup<LoadedAssembly>>.Ok(new Lookup<LoadedAssembly>.NotFound());
        }
        catch (AssemblyLoadException e)
        {
            return new Outcome<Lookup<LoadedAssembly>>.Failed(e);
        }

        return new Outcome<Lookup<LoadedAssembly>>.Ok(new Lookup<LoadedAssembly>.Found { Value = assembly });
    }

    // "Not there" is asked of what the read threw, never assumed of every failure: the file asked for, or the package it
    // is in, missing, or a folder of its path. Another file missing (one it needs), a lock or a corrupt file is a failure.
    // A DirectoryNotFoundException names no file; it is a folder of the path's because the read opens nothing else: the
    // file, its WebCIL or package reading, or the package then a file out of it in memory (Read); the module's
    // resolver and its symbols open files only when first used, after the load.
    private static bool IsNotThere(AssemblyLoadException e, AssemblyPath path)
    {
        return e.InnerException switch
        {
            DirectoryNotFoundException => true,
            FileNotFoundException missing => NamesPathOrPackage(missing.FileName, path),
            _ => false,
        };
    }

    private static bool NamesPathOrPackage(string? file, AssemblyPath path)
    {
        if (file is null)
        {
            return false;
        }

        var named = AssemblyPath.Parse(file);
        if (named is null)
        {
            return false;
        }

        if (named.Equals(path))
        {
            return true;
        }

        var inPackage = AssemblyPackages.SplitPath(path);
        return inPackage is not null && named.Equals(inPackage.Value.Package);
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
            r => ResolveDefinition(list, owner, r.TypeSystem.MainModule, handle, generation),
            u => Lookup.FromUnreadable<(LoadedModule Assembly, EntityHandle Handle)>(owner, u),
            static _ => new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.NotFound());
    }

    private static Lookup<(LoadedModule Assembly, EntityHandle Handle)> ResolveDefinition(AssemblyList list, LoadedModule owner, MetadataModule main,
        EntityHandle handle, int generation)
    {
        var definition = Definitions.Resolve(main, handle);
        if (definition is null)
        {
            ThrowIfInUnreadReference(owner, main.MetadataFile.Metadata, handle);
            return new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.NotFound();
        }

        var assembly = FindAssemblyToLookIn(list, AssemblyPath.Of(DecompilerFacts.GetMetadataFile(definition).FileName), generation);
        if (assembly is null)
        {
            return new Lookup<(LoadedModule Assembly, EntityHandle Handle)>.NotFound();
        }

        return Found(assembly, definition.MetadataToken);
    }

    private const string OwnModule = "";

    // A definition not found may be in a reference the type system read by name only, its file there but unreadable:
    // the lookup needs that file, so it fails with why rather than saying not there. A handle whose reference can't be
    // named (a member of a constructed type) fails with the first of them when any was unread.
    private static void ThrowIfInUnreadReference(LoadedModule owner, MetadataReader metadata, EntityHandle handle)
    {
        var unread = LoadedAssemblies.ListUnreadReferences(owner);
        if (unread.Count == 0)
        {
            return;
        }

        var scope = FindReferenceName(metadata, handle);
        var needed = scope is null ? unread[0] : unread.FirstOrDefault(u => string.Equals(u.Name, scope, StringComparison.OrdinalIgnoreCase));
        if (needed is null)
        {
            return;
        }

        throw new InvalidOperationException($"The definition is in {needed.Name}, whose file {needed.Error.FilePath} could not be read.", needed.Error);
    }

    // The assembly or module a reference handle points into, by the name the module references it by: OwnModule for the
    // module itself, null when the handle names none directly.
    private static string? FindReferenceName(MetadataReader metadata, EntityHandle handle)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeReference:
                var scope = metadata.GetTypeReference((TypeReferenceHandle)handle).ResolutionScope;
                return scope.Kind switch
                {
                    HandleKind.AssemblyReference => metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)scope).Name),
                    HandleKind.ModuleReference => metadata.GetString(metadata.GetModuleReference((ModuleReferenceHandle)scope).Name),
                    HandleKind.TypeReference => FindReferenceName(metadata, (EntityHandle)scope),
                    _ => OwnModule,
                };
            case HandleKind.MemberReference:
                var parent = metadata.GetMemberReference((MemberReferenceHandle)handle).Parent;
                return parent.Kind == HandleKind.TypeReference ? FindReferenceName(metadata, parent) : null;
            case HandleKind.MethodSpecification:
                return FindReferenceName(metadata, metadata.GetMethodSpecification((MethodSpecificationHandle)handle).Method);
            default:
                return null;
        }
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
            throw new AssemblyLoadException(path, $"'{container.FilePath}' holds no '{fullName}'.", new FileNotFoundException($"'{container.FilePath}' holds no '{fullName}'.", path.Text));
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
