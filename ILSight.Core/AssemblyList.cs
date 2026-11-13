using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>
/// The assemblies open in the browser, plus those loaded behind the scenes to resolve references.
/// All of <see cref="AssemblyListing"/> is thread-safe: decompilation and search read it from background threads while the
/// UI thread reads <see cref="Assemblies"/>, which is an immutable snapshot. It keeps its own rule: what it holds
/// changes only through its methods, each holding its lock, which is never held while a file is read; the reads
/// themselves are <see cref="AssemblyListing"/>'s, which hands each read here to be admitted, listed or dropped.
/// </summary>
public sealed class AssemblyList
{
    // The lock is held only by this type's methods, and never while reading files.
    private readonly Guarded<Dictionary<AssemblyPath, LoadedAssembly>, AssemblyListSnapshot> _state = new(new Dictionary<AssemblyPath, LoadedAssembly>(), AssemblyListSnapshot.Empty);

    /// <summary>The list as of now, all of one moment.</summary>
    internal AssemblyListSnapshot Now => _state.Snapshot;

    /// <summary>The open assemblies that loaded, in the order they were opened, with an open package's
    /// assemblies where the package is. A snapshot: it never changes after it is read.</summary>
    public IReadOnlyList<LoadedModule> Assemblies => Now.Loaded;

    /// <summary>The open files in the order they were opened, packages and those that failed to load
    /// (<see cref="FailedAssembly"/>) among them. A snapshot, like <see cref="Assemblies"/>.</summary>
    public IReadOnlyList<LoadedAssembly> Listed => Now.Listed;

    /// <summary>The files asked for that haven't landed, in the order they were asked for (a file asked for twice,
    /// twice): a reopening's, until an assembly is listed for each, it stops arriving or the list is cleared. A
    /// snapshot, like <see cref="Listed"/>; only the type holding the list (<see cref="ClosableAssemblyList"/>) adds
    /// or stops them.</summary>
    public IReadOnlyList<AssemblyPath> Arriving => Now.Arriving;

    /// <summary>Increments whenever <see cref="Assemblies"/> changes.</summary>
    public int Version => Now.Version;

    /// <summary>Increments whenever the list is cleared. Work started before a clear neither lists nor
    /// keeps the assemblies it loads after it, so that what it finds late never lands in the list
    /// that follows.</summary>
    public int Generation => Now.Generation;

    /// <summary>What this machine has installed, which references that nothing else answers fall back on.</summary>
    public required InstalledRuntime Runtime { get; init; }

    /// <summary>The portable PDBs picked for assemblies, which they read in place of the ones they find
    /// themselves.</summary>
    public AssemblyPicks<string> Pdbs { get; } = new();

    /// <summary>The frameworks picked for assemblies, by id such as <c>.NETCoreApp,Version=v8.0</c>,
    /// which their references are resolved against in place of the one they were built for.</summary>
    public AssemblyPicks<TargetFrameworkId> TargetFrameworks { get; } = new();

    /// <summary>Raised after <see cref="Assemblies"/> changes, on the thread that changed it, including
    /// background threads when navigation opens a referenced assembly.</summary>
    public event Action? Changed;

    // Each module listed is added, on whichever thread lists it.
    private readonly ConcurrentQueue<LoadedModule> _typeSystemReadsAsked = new();

    /// <summary>Whether a listed module's type system waits to be read ahead of its first use. Asking allocates
    /// nothing.</summary>
    public bool HasTypeSystemReadsAsked => !_typeSystemReadsAsked.IsEmpty;

    // Only asked here, so that it is ready when first shown: the app's frame edge begins the read
    // (LoadedAssemblies.ReadTypeSystemAsync).
    private void AskTypeSystemRead(LoadedAssembly assembly)
    {
        if (assembly is not LoadedModule module)
        {
            return;
        }

        _typeSystemReadsAsked.Enqueue(module);
    }

    /// <summary>Takes the modules listed since the last take whose type system is to be read ahead of its first use,
    /// for the app's frame edge to begin each read (<see cref="LoadedAssemblies.ReadTypeSystemAsync"/>).</summary>
    public List<LoadedModule> TakeTypeSystemReadsAsked()
    {
        var taken = new List<LoadedModule>();
        while (_typeSystemReadsAsked.TryDequeue(out var module))
        {
            taken.Add(module);
        }

        return taken;
    }

    /// <summary>Whether resolving a reference reads its assembly from a file, rather than only finding one listed.</summary>
    internal bool LoadsReferences => Now.ReadOptions.LoadsReferences;

    /// <summary>Whether the list holds <paramref name="assembly"/>, or the package it was read out of.</summary>
    public bool IsListed(LoadedAssembly assembly)
    {
        return Now.Listed.Contains(assembly.ListedItem);
    }

    /// <summary>Whether any file on the list failed to load. It reads the snapshot's array itself, as
    /// <see cref="AssemblyList.Listed"/> boxes it, so that asking every frame allocates nothing.</summary>
    public bool HasFailedAssemblies
    {
        get
        {
            var listed = Now.Listed;
            for (var i = 0; i < listed.Length; i++)
            {
                if (listed[i] is FailedAssembly)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>True when the assembly or package at <paramref name="filePath"/> is listed and loaded.</summary>
    public bool IsListed(AssemblyPath filePath)
    {
        var listed = Now.Listed;
        for (var i = 0; i < listed.Length; i++)
        {
            if (listed[i] is FailedAssembly)
            {
                continue;
            }

            if (listed[i].FilePath.Equals(filePath))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether the list holds the file at <paramref name="file"/>, landed or on its way: an item listed for it
    /// (failed ones included), a listed package it was read out of, or the file or such a package
    /// <see cref="Arriving"/>.</summary>
    public bool Holds(AssemblyPath file)
    {
        var now = Now;
        foreach (var item in now.Listed)
        {
            if (file.Equals(item.FilePath))
            {
                return true;
            }

            if (item is LoadedPackage && file.IsUnder(item.FilePath))
            {
                return true;
            }
        }

        foreach (var arriving in now.Arriving)
        {
            if (file.Equals(arriving) || file.IsUnder(arriving))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Records <paramref name="paths"/> as on their way, each once more.</summary>
    internal void BeginArriving(IReadOnlyList<AssemblyPath> paths)
    {
        using (var held = _state.Enter())
        {
            var now = held.Snapshot;
            held.Snapshot = now with { Arriving = now.Arriving.AddRange(paths) };
        }
    }

    /// <summary>Takes <paramref name="paths"/> off those on their way, each once; returns those that were still on
    /// their way, which never landed.</summary>
    internal List<AssemblyPath> StopArriving(IReadOnlyList<AssemblyPath> paths)
    {
        var stopped = new List<AssemblyPath>();
        using (var held = _state.Enter())
        {
            var arriving = held.Snapshot.Arriving;
            foreach (var path in paths)
            {
                var index = arriving.IndexOf(path);
                if (index < 0)
                {
                    continue;
                }

                arriving = arriving.RemoveAt(index);
                stopped.Add(path);
            }

            held.Snapshot = held.Snapshot with { Arriving = arriving };
        }
        return stopped;
    }

    /// <summary>The open files' paths, in order, failed ones included, for persisting the list.</summary>
    public IReadOnlyList<AssemblyPath> ListFilePaths()
    {
        return Now.Listed.Select(a => a.FilePath).ToArray();
    }

    /// <summary>Removes the item of the list holding <paramref name="assembly"/>: the assembly, or the package it was
    /// read out of, whose assemblies are forgotten with it, and what was picked for them. Documents and nodes already
    /// handed out keep what they hold. Reads nothing: what stays loaded is what was. Returns the modules it forgot,
    /// for the I/O side to release once nothing reads them.</summary>
    internal RemovedModules Close(LoadedAssembly assembly)
    {
        var item = assembly.ListedItem;
        var removed = new List<LoadedModule>();
        using (var held = _state.Enter())
        {
            if (item is not FailedAssembly && !TryForgetKnown(held.Value, item))
            {
                return RemovedModules.None;
            }

            if (item is LoadedModule itemModule)
            {
                removed.Add(itemModule);
            }

            var isPackage = item is LoadedPackage;
            Pdbs.Forget(item.FilePath, andUnder: isPackage);
            TargetFrameworks.Forget(item.FilePath, andUnder: isPackage);
            removed.AddRange(ForgetReadOutOf(held.Value, item));
            var now = held.Snapshot;
            if (!now.Listed.Contains(item))
            {
                return new RemovedModules { Modules = removed };
            }

            var loaded = now.Loaded.RemoveAll(m => m.ListedItem == item);
            held.Snapshot = now with { Listed = now.Listed.Remove(item), Loaded = loaded, Version = now.Version + 1 };
        }
        Changed?.Invoke();
        return new RemovedModules { Modules = removed };
    }

    /// <summary>Lists <paramref name="fresh"/>, read again for the listed <paramref name="item"/>
    /// (<see cref="AssemblyListing.ReadAgain"/>), where the item is, with the assemblies read out of it, and forgets
    /// the item and the assemblies read out of it, which <paramref name="removed"/> returns for the I/O side to
    /// release. Reads nothing: what is listed is what was read. Nodes already handed out keep what they hold. False,
    /// leaving the list as it is, when the item is no longer listed: then <paramref name="removed"/> is what was read
    /// again, which nothing lists.</summary>
    internal bool TryReplace(LoadedAssembly item, FreshItem fresh, out RemovedModules removed)
    {
        using (var held = _state.Enter())
        {
            var now = held.Snapshot;
            if (!now.Listed.Contains(item))
            {
                removed = fresh.ListModules();
                return false;
            }

            if (!TryForgetKnown(held.Value, item))
            {
                removed = fresh.ListModules();
                return false;
            }

            var forgotten = ForgetReadOutOf(held.Value, item);
            if (item is LoadedModule itemModule)
            {
                forgotten.Insert(0, itemModule);
            }

            removed = new RemovedModules { Modules = forgotten };
            held.Value[fresh.Item.FilePath] = fresh.Item;
            foreach (var assembly in fresh.ReadOutOf)
            {
                held.Value[assembly.FilePath] = assembly;
            }

            var listed = now.Listed.Replace(item, fresh.Item);
            held.Snapshot = now with { Listed = listed, Loaded = ListLoaded(listed, now.Loaded, fresh), Version = now.Version + 1 };
        }
        AskTypeSystemRead(fresh.Item);
        Changed?.Invoke();
        return true;
    }

    private static ImmutableArray<LoadedModule> ListLoaded(ImmutableArray<LoadedAssembly> listed, ImmutableArray<LoadedModule> before,
        FreshItem fresh)
    {
        var loaded = ImmutableArray.CreateBuilder<LoadedModule>();
        foreach (var item in listed)
        {
            if (item != fresh.Item)
            {
                loaded.AddRange(before.Where(m => m.ListedItem == item));
                continue;
            }

            if (item is LoadedModule module)
            {
                loaded.Add(module);
                continue;
            }

            loaded.AddRange(fresh.ReadOutOf);
        }
        return loaded.ToImmutable();
    }

    /// <summary>Closes every assembly, those read behind the scenes to resolve references included, and stops the
    /// opens that started before: they no longer list what they read, and nothing is on its way any more. Returns the
    /// modules it forgot, for the I/O side to release once nothing reads them.</summary>
    internal RemovedModules CloseAll()
    {
        List<LoadedModule> removed;
        using (var held = _state.Enter())
        {
            var now = held.Snapshot with { Generation = held.Snapshot.Generation + 1, Arriving = [] };
            held.Snapshot = now;
            if (now.Listed.IsEmpty && held.Value.Count == 0)
            {
                return RemovedModules.None;
            }

            removed = held.Value.Values.OfType<LoadedModule>().ToList();
            held.Value.Clear();
            held.Snapshot = now with { Listed = [], Loaded = [], Version = now.Version + 1 };
        }
        Changed?.Invoke();
        return new RemovedModules { Modules = removed };
    }

    /// <summary>Orders the open files by name, and those of one name by version; what loaded follows its item.</summary>
    public void SortListed()
    {
        using (var held = _state.Enter())
        {
            var now = held.Snapshot;
            var sorted = now.Listed
                .OrderBy(a => a.Name, NaturalStringComparer.Instance)
                .ThenBy(a => a.Version)
                .ToImmutableArray();
            if (sorted.SequenceEqual(now.Listed))
            {
                return;
            }

            held.Snapshot = now with { Listed = sorted, Loaded = OrderLoaded(now.Loaded, sorted), Version = now.Version + 1 };
        }
        Changed?.Invoke();
    }

    private static ImmutableArray<LoadedModule> OrderLoaded(ImmutableArray<LoadedModule> loaded, ImmutableArray<LoadedAssembly> listed)
    {
        var ordered = ImmutableArray.CreateBuilder<LoadedModule>(loaded.Length);
        foreach (var item in listed)
        {
            foreach (var module in loaded)
            {
                if (module.ListedItem == item)
                {
                    ordered.Add(module);
                }
            }
        }
        return ordered.MoveToImmutable();
    }

    private static bool TryForgetKnown(Dictionary<AssemblyPath, LoadedAssembly> byPath, LoadedAssembly assembly)
    {
        if (!byPath.TryGetValue(assembly.FilePath, out var known))
        {
            return false;
        }

        if (known != assembly)
        {
            return false;
        }

        byPath.Remove(assembly.FilePath);
        return true;
    }

    private static List<LoadedModule> ForgetReadOutOf(Dictionary<AssemblyPath, LoadedAssembly> byPath, LoadedAssembly package)
    {
        var read = byPath.Values
            .OfType<LoadedModule>()
            .Where(module => module.Place is not null && module.Place.Container == package)
            .ToList();
        foreach (var module in read)
        {
            byPath.Remove(module.FilePath);
        }
        return read;
    }

    /// <summary>Takes <paramref name="loadsReferences"/> and <paramref name="readerOptions"/> as the toggles assemblies
    /// are read with from now on. True when they changed: that ends the <see cref="Generation"/>, as a clear does, so
    /// that no assembly read with the toggles before is kept.</summary>
    internal bool TryChangeReadOptions(bool loadsReferences, MetadataReaderOptions readerOptions)
    {
        using (var held = _state.Enter())
        {
            var now = held.Snapshot;
            if (IsSame(now.ReadOptions, loadsReferences, readerOptions))
            {
                return false;
            }

            held.Snapshot = now with { ReadOptions = new AssemblyLoadOptions
            {
                LoadsReferences = loadsReferences,
                ReaderOptions = readerOptions,
            }, Generation = now.Generation + 1 };
        }
        return true;
    }

    /// <summary>Whether <paramref name="options"/> are the toggles <paramref name="loadsReferences"/> and
    /// <paramref name="readerOptions"/>.</summary>
    internal static bool IsSame(AssemblyLoadOptions options, bool loadsReferences, MetadataReaderOptions readerOptions)
    {
        return options.LoadsReferences == loadsReferences && options.ReaderOptions == readerOptions;
    }

    /// <summary>The assembly read from <paramref name="path"/>, listed or not; null when none was read.</summary>
    internal LoadedAssembly? FindRead(AssemblyPath path)
    {
        using (var held = _state.Enter())
        {
            return held.Value.GetValueOrDefault(path);
        }
    }

    /// <summary>Keeps <paramref name="read"/>, read from <paramref name="path"/> for <paramref name="generation"/>, as
    /// what was read from there, unless a clear ended that generation since, or another read of the file was kept
    /// first: then the read is unkept, and what to use is the read, or the one kept first.</summary>
    internal ReadAdmission<LoadedAssembly> Admit(AssemblyPath path, LoadedAssembly read, int generation)
    {
        using (var held = _state.Enter())
        {
            if (generation != held.Snapshot.Generation)
            {
                return new ReadAdmission<LoadedAssembly> { Use = read, Unkept = read };
            }

            if (held.Value.TryGetValue(path, out var existing))
            {
                return new ReadAdmission<LoadedAssembly> { Use = existing, Unkept = read };
            }

            held.Value.Add(path, read);
        }
        return new ReadAdmission<LoadedAssembly> { Use = read, Unkept = null };
    }

    /// <summary>The assembly <paramref name="package"/> holds as <paramref name="path"/>, read out of it before; null
    /// when none was.</summary>
    internal LoadedModule? FindReadOutOf(LoadedPackage package, AssemblyPath path)
    {
        using (var held = _state.Enter())
        {
            return FindReadOutOf(held.Value, package, path);
        }
    }

    /// <summary>Keeps <paramref name="read"/>, read out of <paramref name="package"/> as <paramref name="path"/>, while
    /// the list holds the package; unkept when another read of it was kept first, which is then what to use. One read
    /// out of a package the list no longer holds is used and not kept: it is its package's, released with it.</summary>
    internal ReadAdmission<LoadedModule> AdmitReadOutOf(LoadedPackage package, AssemblyPath path, LoadedModule read)
    {
        using (var held = _state.Enter())
        {
            if (!IsKnown(held.Value, package))
            {
                return new ReadAdmission<LoadedModule> { Use = read, Unkept = null };
            }

            var known = FindReadOutOf(held.Value, package, path);
            if (known is not null)
            {
                return new ReadAdmission<LoadedModule> { Use = known, Unkept = read };
            }

            held.Value[path] = read;
        }
        return new ReadAdmission<LoadedModule> { Use = read, Unkept = null };
    }

    private static LoadedModule? FindReadOutOf(Dictionary<AssemblyPath, LoadedAssembly> byPath, LoadedPackage package, AssemblyPath path)
    {
        if (!byPath.TryGetValue(path, out var known))
        {
            return null;
        }

        if (known is not LoadedModule module || module.Place is null)
        {
            return null;
        }

        if (module.Place.Container != package)
        {
            return null;
        }

        return module;
    }

    private static bool IsKnown(Dictionary<AssemblyPath, LoadedAssembly> byPath, LoadedAssembly assembly)
    {
        if (!byPath.TryGetValue(assembly.FilePath, out var known))
        {
            return false;
        }

        return known == assembly;
    }

    /// <summary>The modules read, listed or not, as of now.</summary>
    internal List<LoadedModule> ListRead()
    {
        using (var held = _state.Enter())
        {
            return held.Value.Values.OfType<LoadedModule>().ToList();
        }
    }

    /// <summary>The newest assembly read of <paramref name="reference"/>'s name, only one as new as it asks
    /// for when <paramref name="exactOrNewer"/>, and only one listed, or read out of a listed package, when
    /// <paramref name="listedOnly"/>; null when there is none.</summary>
    internal LoadedModule? FindKnown(IAssemblyReference reference, bool exactOrNewer, bool listedOnly)
    {
        using (var held = _state.Enter())
        {
            LoadedModule? best = null;
            foreach (var known in held.Value.Values)
            {
                if (known is not LoadedModule assembly)
                {
                    continue;
                }

                if (!string.Equals(assembly.Name, reference.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (listedOnly && !held.Snapshot.Listed.Contains(assembly.ListedItem))
                {
                    continue;
                }

                if (exactOrNewer && reference.Version is not null && assembly.Version is not null && assembly.Version < reference.Version)
                {
                    continue;
                }

                if (best is null || (assembly.Version ?? new Version()) > (best.Version ?? new Version()))
                {
                    best = assembly;
                }
            }
            return best;
        }
    }

    /// <summary>Lists <paramref name="read"/>'s item, with what was read out of it, unless the list was cleared since
    /// <paramref name="generation"/>, <paramref name="cancellationToken"/> was cancelled, or another assembly took its
    /// path; one listed already stops arriving. Reads nothing: what is listed is what was read.</summary>
    internal bool TryList(FreshItem read, int generation, CancellationToken cancellationToken)
    {
        var assembly = read.Item;
        using (var held = _state.Enter())
        {
            if (generation != held.Snapshot.Generation)
            {
                return false;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (!HoldsPathFor(held.Value, held.Snapshot, assembly))
            {
                return false;
            }

            var now = held.Snapshot;
            if (now.Listed.Contains(assembly))
            {
                held.Snapshot = now with { Arriving = WithoutArrival(now.Arriving, assembly.FilePath) };
                return true;
            }

            var listed = ListInPlaceOfFailure(now.Listed, assembly);
            held.Snapshot = now with { Listed = listed, Loaded = ListLoaded(listed, now.Loaded, read), Version = now.Version + 1, Arriving = WithoutArrival(now.Arriving, assembly.FilePath) };
        }
        AskTypeSystemRead(assembly);
        Changed?.Invoke();
        return true;
    }

    private static ImmutableArray<AssemblyPath> WithoutArrival(ImmutableArray<AssemblyPath> arriving, AssemblyPath landed)
    {
        return arriving.RemoveAll(landed.Equals);
    }

    private static bool HoldsPathFor(Dictionary<AssemblyPath, LoadedAssembly> byPath, AssemblyListSnapshot now, LoadedAssembly assembly)
    {
        if (assembly is not FailedAssembly)
        {
            return byPath.TryGetValue(assembly.FilePath, out var known) && known == assembly;
        }

        if (byPath.ContainsKey(assembly.FilePath))
        {
            return false;
        }

        var failure = FindFailure(now.Listed, assembly.FilePath);
        return failure is null || failure == assembly;
    }

    private static ImmutableArray<LoadedAssembly> ListInPlaceOfFailure(ImmutableArray<LoadedAssembly> listed, LoadedAssembly assembly)
    {
        var failure = FindFailure(listed, assembly.FilePath);
        if (failure is null)
        {
            return listed.Add(assembly);
        }

        return listed.Replace(failure, assembly);
    }

    /// <summary>The failure listed for <paramref name="path"/>; null when none is.</summary>
    internal FailedAssembly? FindListedFailure(AssemblyPath path)
    {
        return FindFailure(Now.Listed, path);
    }

    private static FailedAssembly? FindFailure(ImmutableArray<LoadedAssembly> listed, AssemblyPath path)
    {
        foreach (var assembly in listed)
        {
            if (assembly is not FailedAssembly failed)
            {
                continue;
            }

            if (failed.FilePath.Equals(path))
            {
                return failed;
            }
        }
        return null;
    }
}
