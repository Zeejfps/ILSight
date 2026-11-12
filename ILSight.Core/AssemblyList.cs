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
    public InstalledRuntime Runtime { get; internal init; } = new();

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
}
