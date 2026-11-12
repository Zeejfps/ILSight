using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>
/// The assemblies open in the browser, plus those loaded behind the scenes to resolve references.
/// All of <see cref="AssemblyListing"/> is thread-safe: decompilation and search read it from background threads while the
/// UI thread reads <see cref="Assemblies"/>, which is an immutable snapshot.
/// </summary>
public sealed class AssemblyList
{
    /// <summary>What was read, by path, reachable only holding the lock: assemblies, packages and the
    /// assemblies read out of packages. Beside it the <see cref="AssemblyListSnapshot"/>, read without the lock and
    /// replaced only holding it. Hold it only through <see cref="AssemblyListing"/>, and never while reading files.</summary>
    internal Guarded<Dictionary<AssemblyPath, LoadedAssembly>, AssemblyListSnapshot> State { get; } = new(new Dictionary<AssemblyPath, LoadedAssembly>(), AssemblyListSnapshot.Empty);

    /// <summary>The list as of now, all of one moment.</summary>
    internal AssemblyListSnapshot Now => State.Snapshot;

    /// <summary>The open assemblies that loaded, in the order they were opened, with an open package's
    /// assemblies where the package is. A snapshot: it never changes after it is read.</summary>
    public IReadOnlyList<LoadedModule> Assemblies => Now.Loaded;

    /// <summary>The open files in the order they were opened, packages and those that failed to load
    /// (<see cref="FailedAssembly"/>) among them. A snapshot, like <see cref="Assemblies"/>.</summary>
    public IReadOnlyList<LoadedAssembly> Listed => Now.Listed;

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

    /// <summary>Who <see cref="Changed"/> calls, for <see cref="AssemblyListing"/> to call them, as only this
    /// type can raise it.</summary>
    public Action? ChangedHandlers => Changed;

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

    /// <summary>The open files' paths, in order, failed ones included, for persisting the list.</summary>
    public IReadOnlyList<AssemblyPath> ListFilePaths()
    {
        return Now.Listed.Select(a => a.FilePath).ToArray();
    }

    /// <summary>Removes the item of the list holding <paramref name="assembly"/>: the assembly, or the package it was
    /// read out of, whose assemblies are forgotten with it, and what was picked for them. Documents and nodes already
    /// handed out stay valid. Reads nothing: what stays loaded is what was.</summary>
    internal void Close(LoadedAssembly assembly)
    {
        var item = assembly.ListedItem;
        using (var held = State.Enter())
        {
            if (item is not FailedAssembly && !TryForgetKnown(held.Value, item))
            {
                return;
            }

            var isPackage = item is LoadedPackage;
            Pdbs.Forget(item.FilePath, andUnder: isPackage);
            TargetFrameworks.Forget(item.FilePath, andUnder: isPackage);
            ForgetReadOutOf(held.Value, item);
            var now = held.Snapshot;
            if (!now.Listed.Contains(item))
            {
                return;
            }

            var loaded = now.Loaded.RemoveAll(m => m.ListedItem == item);
            held.Snapshot = now with { Listed = now.Listed.Remove(item), Loaded = loaded, Version = now.Version + 1 };
        }
        Changed?.Invoke();
    }

    /// <summary>Closes every assembly, and stops the opens that started before: they no longer list what they
    /// read.</summary>
    internal void CloseAll()
    {
        using (var held = State.Enter())
        {
            var now = held.Snapshot with { Generation = held.Snapshot.Generation + 1 };
            held.Snapshot = now;
            if (now.Listed.IsEmpty && held.Value.Count == 0)
            {
                return;
            }

            held.Value.Clear();
            held.Snapshot = now with { Listed = [], Loaded = [], Version = now.Version + 1 };
        }
        Changed?.Invoke();
    }

    /// <summary>Orders the open files by name, and those of one name by version; what loaded follows its item.</summary>
    public void SortListed()
    {
        using (var held = State.Enter())
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

    /// <summary><paramref name="loaded"/> in the order of the items of <paramref name="listed"/> they were read
    /// from, those of one item as they were.</summary>
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

    /// <summary>Forgets <paramref name="assembly"/> among those read, by path; false when what is known by its path
    /// is not it.</summary>
    internal static bool TryForgetKnown(Dictionary<AssemblyPath, LoadedAssembly> byPath, LoadedAssembly assembly)
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

    /// <summary>Forgets the assemblies read out of <paramref name="package"/> among those read.</summary>
    internal static void ForgetReadOutOf(Dictionary<AssemblyPath, LoadedAssembly> byPath, LoadedAssembly package)
    {
        var read = byPath
            .Where(k => k.Value is LoadedModule module && module.Place is not null && module.Place.Container == package)
            .Select(k => k.Key)
            .ToList();
        foreach (var path in read)
        {
            byPath.Remove(path);
        }
    }
}
