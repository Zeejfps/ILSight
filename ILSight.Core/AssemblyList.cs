using System.Diagnostics;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Metadata;
using AssemblyReference = ICSharpCode.Decompiler.Metadata.AssemblyReference;

namespace ILSight.Core;

/// <summary>Thrown when a file cannot be opened as a .NET assembly or module.</summary>
public sealed class AssemblyLoadException(AssemblyPath filePath, string message, Exception? inner = null) : Exception(message, inner)
{
    public AssemblyPath FilePath { get; } = filePath;
}

/// <summary>
/// The assemblies open in the browser, plus those loaded behind the scenes to resolve references.
/// All of <see cref="AssemblyListing"/> is thread-safe: decompilation and search read it from background threads while the
/// UI thread reads <see cref="Assemblies"/>, which is an immutable snapshot.
/// </summary>
public sealed class AssemblyList
{
    /// <summary>Guards what is changed here: <see cref="Known"/>, what <see cref="LoadedAssembly.IsListed"/> says of
    /// each item, and changing <see cref="ListedNow"/>, <see cref="LoadedNow"/>, <see cref="VersionCount"/>,
    /// <see cref="GenerationCount"/> and <see cref="ReadOptions"/>, which may be read without it. Hold it only
    /// through <see cref="AssemblyListing"/>, and never while reading files.</summary>
    public readonly object Gate = new();

    /// <summary>What was read, by path: assemblies, packages and the assemblies read out of packages.</summary>
    public readonly Dictionary<AssemblyPath, LoadedAssembly> Known = new();

    public ImmutableArray<LoadedAssembly> ListedNow = ImmutableArray<LoadedAssembly>.Empty;
    public ImmutableArray<LoadedModule> LoadedNow = ImmutableArray<LoadedModule>.Empty;
    public int VersionCount;
    public int GenerationCount;
    public volatile LoadOptions ReadOptions = LoadOptions.Default;

    /// <summary>The open assemblies that loaded, in the order they were opened, with an open package's
    /// assemblies where the package is. A snapshot: it never changes after it is read.</summary>
    public IReadOnlyList<LoadedModule> Assemblies => LoadedNow;

    /// <summary>The open files in the order they were opened, packages and those that failed to load
    /// (<see cref="FailedAssembly"/>) among them. A snapshot, like <see cref="Assemblies"/>.</summary>
    public IReadOnlyList<LoadedAssembly> Listed => ListedNow;

    /// <summary>Increments whenever <see cref="Assemblies"/> changes.</summary>
    public int Version => Volatile.Read(ref VersionCount);

    /// <summary>Increments whenever the list is cleared. Work started before a clear neither lists nor
    /// keeps the assemblies it loads after it, so that what it finds late never lands in the list
    /// that follows.</summary>
    public int Generation => Volatile.Read(ref GenerationCount);

    /// <summary>What this machine has installed, which references that nothing else answers fall back on.</summary>
    public InstalledRuntime Runtime { get; init; } = new();

    /// <summary>The portable PDBs picked for assemblies, which they read in place of the ones they find
    /// themselves.</summary>
    public AssemblyPicks<string> Pdbs { get; } = new();

    /// <summary>The frameworks picked for assemblies, by id such as <c>.NETCoreApp,Version=v8.0</c>,
    /// which their references are resolved against in place of the one they were built for.</summary>
    public AssemblyPicks<TargetFrameworkId> TargetFrameworks { get; } = new();

    /// <summary>Raised after <see cref="Assemblies"/> changes, on the thread that changed it, including
    /// background threads when navigation opens a referenced assembly.</summary>
    public event Action? Changed;

    /// <summary>Who <see cref="Changed"/> calls, for <see cref="AssemblyListing"/> to call them, as only this
    /// type can raise it.</summary>
    public Action? ChangedHandlers => Changed;

    /// <summary>Whether resolving a reference reads its assembly from a file, rather than only finding one listed.</summary>
    internal bool LoadsReferences => ReadOptions.LoadsReferences;

    /// <summary>The options that apply as assemblies are read, changed together.</summary>
    public sealed record LoadOptions(bool LoadsReferences, MetadataReaderOptions ReaderOptions)
    {
        public static LoadOptions Default { get; } = new(true, MetadataReaderOptions.ApplyWindowsRuntimeProjections);
    }
}

/// <summary>Opening, reading, listing and closing what an <see cref="AssemblyList"/> holds, and finding what its
/// assemblies refer to. All of this is thread-safe, and it is what keeps the list so: what it changes it changes
/// under the list's gate, and it reads no file while holding that.</summary>
public static class AssemblyListing
{
    /// <summary>Whether the list holds <paramref name="assembly"/>, or the package it was read out of.</summary>
    public static bool IsListed(AssemblyList list, LoadedAssembly assembly)
    {
        return list.ListedNow.Contains(assembly.ListedItem);
    }

    /// <summary>True when the assembly or package at <paramref name="filePath"/> is listed and loaded.</summary>
    public static bool IsListed(AssemblyList list, AssemblyPath filePath)
    {
        var listed = list.ListedNow;
        for (var i = 0; i < listed.Length; i++)
        {
            if (listed[i] is FailedAssembly)
                continue;
            if (listed[i].FilePath.Equals(filePath))
                return true;
        }
        return false;
    }

    /// <summary>The open files' paths, in order, failed ones included, for persisting the list.</summary>
    public static IReadOnlyList<AssemblyPath> ListFilePaths(AssemblyList list)
    {
        return list.ListedNow.Select(a => a.FilePath).ToArray();
    }

    /// <summary>Opens the assembly at <paramref name="path"/>, or returns it if already open. Its type
    /// system starts loading in the background. A file that is missing or not a .NET assembly is listed
    /// all the same, as a <see cref="FailedAssembly"/>, and the assembly takes its place
    /// once the file loads. Once <paramref name="cancellationToken"/> is cancelled
    /// the assembly is no longer listed, so cancelling before <see cref="Clear"/> keeps the opens of
    /// several files out of the list that follows.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled,
    /// or the list was cleared while the file was read.</exception>
    public static LoadedAssembly Open(AssemblyList list, AssemblyPath path, CancellationToken cancellationToken = default)
    {
        var generation = list.Generation;
        while (true)
        {
            var assembly = LoadOrFail(list, path, generation);
            if (TryList(list, assembly, generation, cancellationToken)) return assembly;
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != list.Generation)
                throw new OperationCanceledException($"The list was cleared while '{path}' was opened.");
        }
    }

    /// <summary>The assembly read from <paramref name="path"/>, or when it cannot be read, the failure
    /// listed for it, else a new one.</summary>
    private static LoadedAssembly LoadOrFail(AssemblyList list, AssemblyPath path, int generation)
    {
        try
        {
            return Load(list, path, generation);
        }
        catch (AssemblyLoadException e)
        {
            return FindListedFailure(list, path) ?? LoadedAssemblies.CreateFailed(list, e);
        }
    }

    /// <summary>Removes the item of the list holding <paramref name="assembly"/>: the assembly, or the
    /// package it was read out of, whose assemblies are forgotten with it. Documents and nodes already
    /// handed out stay valid.</summary>
    public static void CloseAssembly(AssemblyList list, LoadedAssembly assembly)
    {
        var item = assembly.ListedItem;
        lock (list.Gate)
        {
            if (item is not FailedAssembly && !ForgetKnown(list, item)) return;
            var isPackage = item is LoadedPackage;
            list.Pdbs.Forget(item.FilePath, andUnder: isPackage);
            list.TargetFrameworks.Forget(item.FilePath, andUnder: isPackage);
            ForgetReadOutOf(list, item);
            if (!item.IsListed) return;
            item.IsListed = false;
            ChangeListed(list, list.ListedNow.Remove(item));
        }
        list.ChangedHandlers?.Invoke();
    }

    private static bool ForgetKnown(AssemblyList list, LoadedAssembly assembly)
    {
        if (!list.Known.TryGetValue(assembly.FilePath, out var known))
            return false;
        if (known != assembly)
            return false;
        list.Known.Remove(assembly.FilePath);
        return true;
    }

    private static void ForgetReadOutOf(AssemblyList list, LoadedAssembly package)
    {
        var read = list.Known
            .Where(k => k.Value is LoadedModule module && module.Place != null && module.Place.Container == package)
            .Select(k => k.Key)
            .ToList();
        foreach (var path in read)
            list.Known.Remove(path);
    }

    private static void ChangeListed(AssemblyList list, ImmutableArray<LoadedAssembly> listed)
    {
        list.ListedNow = listed;
        list.LoadedNow = ListLoaded(listed);
        list.VersionCount++;
    }

    private static ImmutableArray<LoadedModule> ListLoaded(ImmutableArray<LoadedAssembly> listed)
    {
        var loaded = ImmutableArray.CreateBuilder<LoadedModule>();
        foreach (var item in listed)
        {
            switch (item)
            {
                case FailedAssembly:
                    break;
                case LoadedPackage package:
                    loaded.AddRange(LoadedAssemblies.EnsureAssemblies(package));
                    break;
                case LoadedModule module:
                    loaded.Add(module);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
        return loaded.ToImmutable();
    }

    /// <summary>Takes from <paramref name="options"/> the toggles that apply as assemblies are read: whether
    /// references load their assemblies, and whether Windows Runtime metadata is projected. Returns true
    /// when they changed, so that the open assemblies must be read again to follow them. A change ends the
    /// <see cref="Generation"/>, as a clear does, so that no assembly read with the options before is kept.</summary>
    public static bool UseLoadOptions(AssemblyList list, DecompilerOptions options)
    {
        var loadsReferences = DecompilerToggles.IsOn(options, DecompilerToggle.AutoLoadAssemblyReferences);
        var readerOptions = ChooseReaderOptions(options);
        if (IsSame(list.ReadOptions, loadsReferences, readerOptions))
            return false;
        lock (list.Gate)
        {
            if (IsSame(list.ReadOptions, loadsReferences, readerOptions))
                return false;
            list.ReadOptions = new AssemblyList.LoadOptions(loadsReferences, readerOptions);
            list.GenerationCount++;
        }
        return true;
    }

    private static MetadataReaderOptions ChooseReaderOptions(DecompilerOptions options)
    {
        if (DecompilerToggles.IsOn(options, DecompilerToggle.ApplyWindowsRuntimeProjections))
            return MetadataReaderOptions.ApplyWindowsRuntimeProjections;
        return MetadataReaderOptions.None;
    }

    /// <summary>Closes every assembly.</summary>
    public static void CloseAll(AssemblyList list)
    {
        lock (list.Gate)
        {
            list.GenerationCount++;
            if (list.ListedNow.IsEmpty && list.Known.Count == 0) return;
            foreach (var assembly in list.ListedNow)
                assembly.IsListed = false;
            list.Known.Clear();
            ChangeListed(list, ImmutableArray<LoadedAssembly>.Empty);
        }
        list.ChangedHandlers?.Invoke();
    }

    /// <summary>Reads the item of the list holding <paramref name="assembly"/> again from its file: the
    /// assembly, or the package it was read out of with the assemblies in it, each taking what is picked
    /// for it now. It is not listed until <see cref="Replace"/> puts it in the item's place. Reads the
    /// file: call it off the UI thread.</summary>
    /// <exception cref="AssemblyLoadException">The file can no longer be read.</exception>
    public static LoadedAssembly ReadAgain(AssemblyList list, LoadedAssembly assembly)
    {
        var fresh = ReadAssemblyOrPackage(list, assembly.ListedItem.FilePath);
        if (fresh is LoadedPackage package)
            LoadedAssemblies.EnsureAssemblies(package);
        return fresh;
    }

    /// <summary>Lists <paramref name="fresh"/>, which <see cref="ReadAgain"/> read for the listed
    /// <paramref name="item"/>, where the item is, and forgets the item and the assemblies read out of
    /// it. Nodes already handed out stay valid. False, leaving the list as it is, when the item is no
    /// longer listed.</summary>
    public static bool Replace(AssemblyList list, LoadedAssembly item, LoadedAssembly fresh)
    {
        lock (list.Gate)
        {
            if (!item.IsListed) return false;
            if (!ForgetKnown(list, item)) return false;
            ForgetReadOutOf(list, item);
            Know(list, fresh);
            item.IsListed = false;
            fresh.IsListed = true;
            ChangeListed(list, list.ListedNow.Replace(item, fresh));
        }
        WarmUpLater(fresh);
        list.ChangedHandlers?.Invoke();
        return true;
    }

    private static void Know(AssemblyList list, LoadedAssembly item)
    {
        list.Known[item.FilePath] = item;
        if (item is not LoadedPackage package)
            return;
        foreach (var assembly in LoadedAssemblies.EnsureAssemblies(package))
            list.Known[assembly.FilePath] = assembly;
    }

    /// <summary>Orders the open files by name, and those of one name by version.</summary>
    public static void SortListed(AssemblyList list)
    {
        lock (list.Gate)
        {
            var sorted = list.ListedNow
                .OrderBy(a => a.Name, NaturalStringComparer.Instance)
                .ThenBy(a => a.Version)
                .ToImmutableArray();
            if (sorted.SequenceEqual(list.ListedNow))
                return;
            ChangeListed(list, sorted);
        }
        list.ChangedHandlers?.Invoke();
    }

    /// <summary>Lists the assemblies <paramref name="assembly"/> references that can be found, as
    /// following a reference to them does, and returns them. Those that cannot be found are left out.
    /// May load files: call it off the UI thread.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static List<LoadedModule> ListReferences(AssemblyList list, LoadedModule assembly, CancellationToken cancellationToken = default)
    {
        var generation = list.Generation;
        var listed = new List<LoadedModule>();
        foreach (var reference in assembly.File.AssemblyReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = AssemblyResolving.LoadAssembly(assembly.Resolver, reference);
            if (found == null)
                continue;
            if (TryList(list, found, generation, cancellationToken))
                listed.Add(found);
        }
        return listed;
    }

    /// <summary>The listed assembly that defines what <paramref name="reference"/> points at, and the
    /// handle of its definition there: the assembly's own handle for an assembly. A null assembly
    /// when it cannot be resolved or listed.</summary>
    internal static (LoadedModule? Assembly, EntityHandle Handle) ResolveListed(AssemblyList list, CodeReference.Navigable reference)
    {
        var generation = list.Generation;
        var owner = TryLoadAssembly(list, reference.FilePath, generation);
        if (owner is null)
            return default;
        var (assembly, handle) = ResolveTarget(list, owner, reference, generation);
        if (assembly is null)
            return default;
        if (!TryList(list, assembly, generation))
            return default;
        return (assembly, handle);
    }

    private static (LoadedModule? Assembly, EntityHandle Handle) ResolveTarget(AssemblyList list, LoadedModule owner, CodeReference.Navigable reference, int generation)
    {
        switch (reference)
        {
            case CodeReference.Entity entity:
                return ResolveDefinition(list, owner, entity.Handle, generation);
            case CodeReference.Assembly assembly:
                if (assembly.Referenced == null)
                    return (owner, EntityHandle.AssemblyDefinition);
                var target = AssemblyResolving.ResolveAssembly(owner.Resolver, new AssemblyReference(owner.File, assembly.Referenced.Value));
                return (target, EntityHandle.AssemblyDefinition);
            default:
                throw new UnreachableException();
        }
    }

    internal static bool IsUnreadable(Exception e)
    {
        return e is BadImageFormatException or IOException;
    }

    /// <summary>
    /// What hovering <paramref name="reference"/> explains: the declaration and XML documentation of
    /// the type or member it points at. Null when it cannot be resolved. Loads the files it needs without listing them, and reads documentation files:
    /// call it off the UI thread.
    /// </summary>
    public static CodeTooltip? DescribeReference(AssemblyList list, CodeReference.Entity reference)
    {
        try
        {
            var (entity, assembly) = ResolveEntity(list, reference);
            if (entity is null || assembly is null)
                return null;
            return Documentation.CreateTooltip(entity, assembly);
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            return null;
        }
    }

    /// <summary>
    /// The page of the .NET API reference about the type or member <paramref name="reference"/>
    /// points at, as <see cref="MicrosoftDocs.FindPage(BrowseNode)"/> names it. Null for what is not
    /// visible outside its assembly, or when it cannot be resolved. Loads the files it needs without listing them: call it off the UI thread.
    /// </summary>
    public static string? FindApiReferencePage(AssemblyList list, CodeReference.Entity reference)
    {
        try
        {
            var (entity, _) = ResolveEntity(list, reference);
            if (entity is null)
                return null;
            return MicrosoftDocs.FindPage(entity);
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            return null;
        }
    }

    /// <summary>The definition of the type <paramref name="reference"/> points at, loading the assembly
    /// that defines it without listing it; null when it points at no type, or cannot be resolved.
    /// Call it off the UI thread.</summary>
    internal static ITypeDefinition? FindTypeDefinition(AssemblyList list, CodeReference.Entity reference)
    {
        if (reference.Kind != EntityKind.Type)
            return null;
        try
        {
            var (entity, _) = ResolveEntity(list, reference);
            return entity as ITypeDefinition;
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            return null;
        }
    }

    /// <summary>The definition of the type <paramref name="anchor"/> names, loading the assembly that
    /// defines it without listing it; null when its file cannot be read or no longer defines it. Call it
    /// off the UI thread.</summary>
    internal static ITypeDefinition? FindTypeDefinition(AssemblyList list, TypeAnchor anchor)
    {
        try
        {
            var assembly = TryLoadAssembly(list, anchor.AssemblyPath);
            if (assembly is null)
                return null;
            return LoadedAssemblies.EnsureMainModule(assembly).GetTypeDefinition(new FullTypeName(anchor.FullName));
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            return null;
        }
    }

    /// <summary>The type or member <paramref name="reference"/> points at and the assembly that defines
    /// it, loading that assembly without listing it; nulls when it cannot be resolved.</summary>
    private static (IEntity? Entity, LoadedModule? Assembly) ResolveEntity(AssemblyList list, CodeReference.Entity entity)
    {
        var generation = list.Generation;
        var owner = TryLoadAssembly(list, entity.FilePath, generation);
        if (owner is null)
            return default;
        var (assembly, handle) = ResolveDefinition(list, owner, entity.Handle, generation);
        if (assembly is null)
            return default;
        return (LoadedAssemblies.EnsureMainModule(assembly).ResolveEntity(handle), assembly);
    }

    /// <summary>The listed item <paramref name="path"/> names: its assembly, opened and listed if it is not
    /// and its file still exists, or the file listed as failed there. Null if the file cannot be opened,
    /// or the list was cleared meanwhile. May load files: call it off the UI thread.</summary>
    internal static LoadedAssembly? OpenListed(AssemblyList list, AssemblyPath path)
    {
        var generation = list.Generation;
        var assembly = TryLoad(list, path, generation) ?? FindListedFailure(list, path);
        if (assembly is null || !TryList(list, assembly, generation))
            return null;
        return assembly;
    }

    /// <summary>The item the list holds now that was read from <paramref name="path"/>; null when it
    /// holds none there, or only a file that failed.</summary>
    internal static LoadedAssembly? FindListedLoaded(AssemblyList list, AssemblyPath path)
    {
        var listed = list.ListedNow;
        foreach (var assembly in listed)
        {
            if (assembly is FailedAssembly)
                continue;
            if (assembly.FilePath.Equals(path))
                return assembly;
        }
        return null;
    }

    internal static LoadedAssembly? TryLoad(AssemblyList list, AssemblyPath path)
    {
        return TryLoad(list, path, list.Generation);
    }

    /// <summary>The assembly at <paramref name="path"/>, as <see cref="TryLoad(AssemblyPath, int)"/> loads it;
    /// null for a package, which has no metadata of its own, or a file that failed to load.</summary>
    internal static LoadedModule? TryLoadAssembly(AssemblyList list, AssemblyPath path)
    {
        return TryLoadAssembly(list, path, list.Generation);
    }

    private static LoadedModule? TryLoadAssembly(AssemblyList list, AssemblyPath path, int generation)
    {
        return TryLoad(list, path, generation) is LoadedModule module ? module : null;
    }

    internal static LoadedAssembly? TryLoad(AssemblyList list, AssemblyPath path, int generation)
    {
        try
        {
            return Load(list, path, generation);
        }
        catch (AssemblyLoadException)
        {
            return null;
        }
    }

    /// <summary>The type system of the assembly loaded from <paramref name="module"/>.</summary>
    internal static DecompilerTypeSystem EnsureTypeSystemOf(AssemblyList list, MetadataFile module)
    {
        return LoadedAssemblies.EnsureTypeSystem(FindLoadedFrom(list, module));
    }

    private static LoadedModule FindLoadedFrom(AssemblyList list, MetadataFile module)
    {
        lock (list.Gate)
        {
            foreach (var assembly in list.Known.Values)
            {
                if (assembly is LoadedModule loaded && loaded.File == module)
                    return loaded;
            }
        }
        throw new InvalidOperationException($"{module.FileName} was not loaded by list list.");
    }

    /// <summary>The newest assembly read of <paramref name="reference"/>'s name, only one as new as it asks
    /// for when <paramref name="exactOrNewer"/>, and only one listed, or read out of a listed package, when
    /// <paramref name="listedOnly"/>; null when there is none.</summary>
    internal static LoadedModule? FindKnown(AssemblyList list, IAssemblyReference reference, bool exactOrNewer, bool listedOnly = false)
    {
        lock (list.Gate)
        {
            LoadedModule? best = null;
            foreach (var known in list.Known.Values)
            {
                if (known is not LoadedModule assembly) continue;
                if (listedOnly && !assembly.ListedItem.IsListed) continue;
                if (!string.Equals(assembly.Name, reference.Name, StringComparison.OrdinalIgnoreCase)) continue;
                if (exactOrNewer && reference.Version != null && assembly.Version != null && assembly.Version < reference.Version) continue;
                if (best is null || (assembly.Version ?? new Version()) > (best.Version ?? new Version()))
                    best = assembly;
            }
            return best;
        }
    }

    /// <summary>The assembly at <paramref name="path"/>, read from the file unless it is loaded. One read
    /// for a <paramref name="generation"/> that a clear has ended is not kept.</summary>
    private static LoadedAssembly Load(AssemblyList list, AssemblyPath path, int generation)
    {
        lock (list.Gate)
        {
            if (list.Known.TryGetValue(path, out var existing)) return existing;
        }

        var assembly = Read(list, path, generation);
        lock (list.Gate)
        {
            if (generation != list.GenerationCount) return assembly;
            if (list.Known.TryGetValue(path, out var existing)) return existing;
            list.Known.Add(path, assembly);
        }
        return assembly;
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
        if (assembly is LoadedModule module && module.Place != null)
            return TryList(list, module.Place.Container, generation, cancellationToken);
        if (assembly is LoadedPackage package)
            LoadedAssemblies.EnsureAssemblies(package);
        lock (list.Gate)
        {
            if (generation != list.GenerationCount) return false;
            if (cancellationToken.IsCancellationRequested) return false;
            if (!HoldsPathFor(list, assembly)) return false;
            if (assembly.IsListed) return true;
            assembly.IsListed = true;
            ChangeListed(list, ListInPlaceOfFailure(list, assembly));
        }
        WarmUpLater(assembly);
        list.ChangedHandlers?.Invoke();
        return true;
    }

    /// <summary>Loads a listed assembly's type system in the background, so that it is ready when
    /// first shown.</summary>
    private static void WarmUpLater(LoadedAssembly assembly)
    {
        if (assembly is not LoadedModule module)
            return;
        ThreadPool.QueueUserWorkItem(static m => LoadedAssemblies.WarmUp(m), module, preferLocal: false);
    }

    /// <summary>The assembly <paramref name="package"/> holds as <paramref name="file"/>: the one read
    /// before, else one read now, kept while the list holds the package. Null when it does not load.</summary>
    internal static LoadedModule? TryLoadOutOf(AssemblyList list, LoadedPackage package, PackageFile file)
    {
        var path = AssemblyPackages.GetFilePath(package.Package, file);
        lock (list.Gate)
        {
            var known = FindReadOutOf(list, package, path);
            if (known != null) return known;
        }
        var read = TryReadOutOfPackage(list, package, path, file.FullName);
        if (read == null)
            return null;
        lock (list.Gate)
        {
            if (!IsKnown(list, package)) return read;
            var known = FindReadOutOf(list, package, path);
            if (known != null) return known;
            list.Known[path] = read;
        }
        return read;
    }

    private static LoadedModule? FindReadOutOf(AssemblyList list, LoadedPackage package, AssemblyPath path)
    {
        if (!list.Known.TryGetValue(path, out var known))
            return null;
        if (known is not LoadedModule module || module.Place == null)
            return null;
        if (module.Place.Container != package)
            return null;
        return module;
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

    private static bool IsKnown(AssemblyList list, LoadedAssembly assembly)
    {
        if (!list.Known.TryGetValue(assembly.FilePath, out var known))
            return false;
        return known == assembly;
    }

    /// <summary>Whether <paramref name="assembly"/> is what the list holds for its path: the assembly
    /// read from it, or for a failure, the one listed for it while nothing was read from it.</summary>
    private static bool HoldsPathFor(AssemblyList list, LoadedAssembly assembly)
    {
        if (assembly is not FailedAssembly)
            return list.Known.TryGetValue(assembly.FilePath, out var known) && known == assembly;
        if (list.Known.ContainsKey(assembly.FilePath))
            return false;
        var listed = FindListedFailure(list, assembly.FilePath);
        return listed == null || listed == assembly;
    }

    /// <summary>The list with <paramref name="assembly"/> where the failure listed for its path was,
    /// or last when there is none.</summary>
    private static ImmutableArray<LoadedAssembly> ListInPlaceOfFailure(AssemblyList list, LoadedAssembly assembly)
    {
        var failure = FindListedFailure(list, assembly.FilePath);
        if (failure == null)
            return list.ListedNow.Add(assembly);
        failure.IsListed = false;
        return list.ListedNow.Replace(failure, assembly);
    }

    private static FailedAssembly? FindListedFailure(AssemblyList list, AssemblyPath path)
    {
        var listed = list.ListedNow;
        foreach (var assembly in listed)
        {
            if (assembly is not FailedAssembly failed)
                continue;
            if (failed.FilePath.Equals(path))
                return failed;
        }
        return null;
    }

    private static (LoadedModule? Assembly, EntityHandle Handle) ResolveDefinition(AssemblyList list, LoadedModule owner, EntityHandle handle, int generation)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition or HandleKind.MethodDefinition or HandleKind.FieldDefinition
                or HandleKind.PropertyDefinition or HandleKind.EventDefinition:
                return (owner, handle);
        }

        var definition = Definitions.Resolve(LoadedAssemblies.EnsureMainModule(owner), handle);
        if (definition == null)
            return (null, default);
        return (TryLoadAssembly(list, AssemblyPath.Of(definition.ParentModule!.MetadataFile!.FileName), generation), definition.MetadataToken);
    }

    /// <summary>What <paramref name="path"/> holds: an assembly, a package, or an assembly in a
    /// package, read from a path <see cref="AssemblyPackage.PathOf"/> made.</summary>
    /// <exception cref="AssemblyLoadException">There is no such file, or it is none of those.</exception>
    private static LoadedAssembly Read(AssemblyList list, AssemblyPath path, int generation)
    {
        if (File.Exists(path.Text))
            return ReadAssemblyOrPackage(list, path);
        var inPackage = AssemblyPackages.SplitPath(path);
        if (inPackage == null)
            return LoadedAssemblies.CreateModule(list, path, ReadFile(list, path));
        var container = Load(list, inPackage.Value.Package, generation);
        if (container is not LoadedPackage package)
            throw new AssemblyLoadException(path, $"'{container.FilePath}' is not a package.");
        return ReadOutOfPackage(list, package, path, inPackage.Value.FullName);
    }

    /// <summary>The assembly at <paramref name="path"/>, else the WebCIL module, else the package, else
    /// why it is none of those.</summary>
    private static LoadedAssembly ReadAssemblyOrPackage(AssemblyList list, AssemblyPath path)
    {
        try
        {
            return LoadedAssemblies.CreateModule(list, path, ReadFile(list, path));
        }
        catch (AssemblyLoadException)
        {
            var read = ReadWebCilOrPackage(list, path);
            if (read == null)
                throw;
            return read;
        }
    }

    private static LoadedAssembly? ReadWebCilOrPackage(AssemblyList list, AssemblyPath path)
    {
        var module = ReadWebCil(list, path);
        if (module != null)
            return LoadedAssemblies.CreateModule(list, path, module);
        var package = ReadPackage(path);
        if (package == null)
            return null;
        return LoadedAssemblies.CreatePackage(list, package);
    }

    /// <summary>The assembly <paramref name="path"/> holds as WebCIL, wrapped in WebAssembly the way
    /// Blazor ships it; null when the file is no WebCIL.</summary>
    private static WebCilFile? ReadWebCil(AssemblyList list, AssemblyPath path)
    {
        try
        {
            return WebCilFile.FromFile(path.Text, list.ReadOptions.ReaderOptions);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException)
        {
            throw new AssemblyLoadException(path, $"Cannot open '{path}': {e.Message}", e);
        }
    }

    private static AssemblyPackage? ReadPackage(AssemblyPath path)
    {
        try
        {
            return AssemblyPackages.TryRead(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new AssemblyLoadException(path, $"Cannot open '{path}': {e.Message}", e);
        }
    }

    private static LoadedModule ReadOutOfPackage(AssemblyList list, LoadedPackage container, AssemblyPath path, string fullName)
    {
        var package = container.Package;
        var file = AssemblyPackages.FindFile(package, fullName);
        if (file == null)
            throw new AssemblyLoadException(path, $"'{container.FilePath}' holds no '{fullName}'.");
        var image = ReadPackageFile(list, package, file);
        return LoadedAssemblies.CreateModule(list, path, image, new PackagePlace(container, file));
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
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new AssemblyLoadException(path, $"Cannot open '{path}': {e.Message}", e);
        }
    }

    private static PEFile ReadFile(AssemblyList list, AssemblyPath path)
    {
        try
        {
            using var stream = new FileStream(path.Text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return ReadImage(list, path, stream);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new AssemblyLoadException(path, $"Cannot open '{path}': {e.Message}", e);
        }
    }

    /// <summary>The assembly or module <paramref name="stream"/> holds, known by <paramref name="path"/>.</summary>
    private static PEFile ReadImage(AssemblyList list, AssemblyPath path, Stream stream)
    {
        try
        {
            var file = new PEFile(path.Text, stream, PEStreamOptions.PrefetchEntireImage, list.ReadOptions.ReaderOptions);
            if (!file.Reader.HasMetadata)
                throw new AssemblyLoadException(path, $"'{path}' is not a .NET assembly.");
            return file;
        }
        catch (Exception e) when (e is BadImageFormatException or InvalidOperationException or MetadataFileNotSupportedException)
        {
            throw new AssemblyLoadException(path, $"Cannot open '{path}': {e.Message}", e);
        }
    }

    private static bool IsSame(AssemblyList.LoadOptions options, bool loadsReferences, MetadataReaderOptions readerOptions)
    {
        return options.LoadsReferences == loadsReferences && options.ReaderOptions == readerOptions;
    }
}
