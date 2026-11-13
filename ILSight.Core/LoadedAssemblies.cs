using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.DebugInfo;
using ILSight.Core.Metadata;

namespace ILSight.Core;

/// <summary>Making what an <see cref="AssemblyList"/> holds, and asking it for what takes reading.</summary>
public static class LoadedAssemblies
{
    private static readonly TypeSystemOptions DefaultTypeSystemOptions = DecompilerTypeSystem.GetOptions(Settings.Create());

    /// <summary>The library's objects of each module made here, kept against the module, so they live as long as it
    /// does, and released once it is closed and nothing reads it (<see cref="Release"/>).</summary>
    private static readonly ConditionalWeakTable<LoadedModule, ModuleLibrary> Libraries = new();

    /// <summary>The module <paramref name="file"/> holds, known by <paramref name="path"/>, with nothing
    /// but its headers read yet; the library's objects kept against it.</summary>
    internal static LoadedModule CreateModule(AssemblyList list, AssemblyPath path, MetadataFile file, PackagePlace? place = null)
    {
        var name = GetModuleName(path, file);
        var version = FindVersion(file);
        var detected = TryDetectTargetFramework(file);
        var picked = list.TargetFrameworks.Find(path);
        var targetFramework = picked ?? detected;
        var resolver = AssemblyResolver.Create(list, path, targetFramework, place?.Container.Package);
        var module = new LoadedModule
        {
            List = list,
            FilePath = path,
            Name = name,
            FullName = GetFullName(file),
            Version = version,
            NameAndVersion = JoinNameAndVersion(name, version),
            Place = place,
            DetectedTargetFramework = detected,
            TargetFramework = targetFramework,
            IsTargetFrameworkPicked = picked is not null,
            CanGeneratePdb = file is PEFile pe && DebugSymbolFiles.CanGenerateFor(pe),
            CanPickPdb = file is PEFile,
            IsImageInMemory = file is PEFile,
        };
        Libraries.Add(module, new ModuleLibrary(file, resolver, () => FindDebugSymbols(list, path, file, place), () => ForwardedTypes.ReadUsage(file.Metadata)));
        return module;
    }

    /// <summary>The library's objects kept against <paramref name="module"/>, to read; throws for one not made here, or
    /// released: a unit reads a module only under a reading (<see cref="TryBeginReading(LoadedModule)"/>).</summary>
    private static ModuleLibrary GetLibrary(LoadedModule module)
    {
        var library = GetKept(module);
        if (library.IsReleased)
        {
            throw new InvalidOperationException($"The module '{module.FilePath}' was read after its release: the unit reading it holds no reading of it.");
        }

        return library;
    }

    /// <summary>The library's objects kept against <paramref name="module"/>, released or not; throws for one not made
    /// here.</summary>
    internal static ModuleLibrary GetKept(LoadedModule module)
    {
        if (!Libraries.TryGetValue(module, out var library))
        {
            throw new InvalidOperationException($"The module '{module.FilePath}' was not read by {nameof(LoadedAssemblies)}.");
        }

        return library;
    }

    /// <summary>Begins a unit's reading, of no module yet: the unit adds each module as it comes to read it
    /// (<see cref="TryRead"/>, <see cref="ReadTypeSystemAsync"/>) and disposes it however it ends.</summary>
    public static ModuleReading BeginReading()
    {
        return new ModuleReading();
    }

    /// <summary>Begins a unit's reading of <paramref name="module"/>, which the unit disposes however it ends; null once
    /// the module was closed, which the unit answers as closed. While it is open, the module isn't released, closed or
    /// not.</summary>
    public static ModuleReading? TryBeginReading(LoadedModule module)
    {
        var reading = BeginReading();
        if (!TryRead(reading, module))
        {
            reading.Dispose();
            return null;
        }

        return reading;
    }

    /// <summary>Adds <paramref name="module"/> to what <paramref name="reading"/> reads, so it isn't released before the
    /// unit ends; false once it was closed, unless the unit already reads it, which the unit answers as closed, or
    /// passes over.</summary>
    public static bool TryRead(ModuleReading reading, LoadedModule module)
    {
        return reading.TryAdd(GetKept(module));
    }

    /// <summary>Releases what <paramref name="removed"/> took off the list: marks each module closed, and lets its
    /// library's objects go now when no reading of it is open, else when the last one ends (a WebCIL module's mapped
    /// file disposed, a <see cref="PEFile"/> left to the collector).</summary>
    public static void Release(RemovedModules removed)
    {
        foreach (var module in removed.Modules)
        {
            GetKept(module).Close();
        }
    }

    /// <summary>Completes once <paramref name="module"/>'s library objects are released: what a path that needs its file
    /// free once it is closed (to replace or delete it) awaits after the close.</summary>
    public static Task WaitForReleaseAsync(LoadedModule module, CancellationToken ct)
    {
        return GetKept(module).Released.WaitAsync(ct);
    }

    /// <summary>The resolver of what <paramref name="module"/> references, for the library to ask within the unit of
    /// <paramref name="reading"/>: what it hands out is added to that reading.</summary>
    internal static IAssemblyResolver CreateResolver(ModuleReading reading, LoadedModule module)
    {
        return new ReadingResolver(GetLibrary(module), reading);
    }

    /// <summary>The library's file <paramref name="module"/> was read from.</summary>
    internal static MetadataFile GetFile(LoadedModule module)
    {
        return GetLibrary(module).File;
    }

    /// <summary><see cref="GetFile"/> as a portable executable; null for a WebCIL module, which has no PE
    /// headers.</summary>
    internal static PEFile? FindPEFile(LoadedModule module)
    {
        return GetFile(module) as PEFile;
    }

    /// <summary>What finds what <paramref name="module"/> references, for the list.</summary>
    internal static AssemblyResolver GetResolver(LoadedModule module)
    {
        return GetLibrary(module).Resolver;
    }

    /// <summary>Whether <paramref name="module"/> was read from <paramref name="file"/>, the very one. Compares the
    /// objects, reading neither, so it asks after the module's release too.</summary>
    internal static bool IsReadFrom(LoadedModule module, MetadataFile file)
    {
        return ReferenceEquals(GetKept(module).File, file);
    }

    /// <summary>Whether <paramref name="folder"/> shows the metadata of its module's own file, rather than that of its
    /// debug symbols. Compares the objects, reading neither, so it asks after the module's release too.</summary>
    internal static bool IsOwnMetadata(MetadataFolderInfo folder)
    {
        return ReferenceEquals(folder.File, GetKept(folder.Module).File);
    }

    /// <summary>The failure listed for the file <paramref name="error"/> tells of.</summary>
    internal static FailedAssembly CreateFailed(AssemblyList list, AssemblyLoadException error)
    {
        var path = error.FilePath;
        var name = GetFileName(path);
        return new FailedAssembly
        {
            List = list,
            FilePath = path,
            Name = name,
            FullName = path.Text,
            Version = null,
            NameAndVersion = JoinNameAndVersion(name, null),
            Error = error,
        };
    }

    /// <summary>The item of <paramref name="list"/> browsing <paramref name="package"/>, with none of its
    /// assemblies read yet. Reading them needs the item itself, which is what they say they came out of,
    /// so that is assigned after it is made.</summary>
    internal static LoadedPackage CreatePackage(AssemblyList list, AssemblyPackage package)
    {
        var path = package.FilePath;
        var name = GetFileName(path);
        var created = new LoadedPackage
        {
            List = list,
            FilePath = path,
            Name = name,
            FullName = path.Text,
            Version = null,
            NameAndVersion = JoinNameAndVersion(name, null),
            Package = package,
        };
        created.Assemblies = new Lazy<IReadOnlyList<LoadedModule>>(() => ListAssemblies(created));
        return created;
    }

    private static string JoinNameAndVersion(string name, Version? version)
    {
        if (version is null)
        {
            return name;
        }

        return $"{name} ({version})";
    }

    /// <summary>The name of a file that holds no assembly's name: the file's, without its extension.</summary>
    private static string GetFileName(AssemblyPath filePath)
    {
        return Path.GetFileNameWithoutExtension(filePath.Text);
    }

    /// <summary>The assembly's name, or for a module that is no assembly, its file's.</summary>
    private static string GetModuleName(AssemblyPath path, MetadataFile file)
    {
        if (!file.Metadata.IsAssembly)
        {
            return GetFileName(path);
        }

        var metadata = file.Metadata;
        return metadata.GetString(metadata.GetAssemblyDefinition().Name);
    }

    private static string GetFullName(MetadataFile file)
    {
        if (!file.Metadata.IsAssembly)
        {
            return file.Name;
        }

        return file.FullName;
    }

    private static Version? FindVersion(MetadataFile file)
    {
        if (!file.Metadata.IsAssembly)
        {
            return null;
        }

        return file.Metadata.GetAssemblyDefinition().Version;
    }

    private static TargetFrameworkId? TryDetectTargetFramework(MetadataFile file)
    {
        try
        {
            return TargetFrameworkId.Parse(file.DetectTargetFrameworkId());
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>The symbols of the PDB the list picked for this assembly, else those it finds itself.</summary>
    private static DebugSymbols? FindDebugSymbols(AssemblyList list, AssemblyPath path, MetadataFile file, PackagePlace? place)
    {
        if (file is not PEFile pe)
        {
            return null;
        }

        var picked = list.Pdbs.Find(path);
        if (picked is null)
        {
            return DebugSymbolFiles.FindSymbols(pe, place?.Container.Package);
        }

        return DebugSymbolFiles.ReadPicked(picked);
    }


    /// <summary>The assembly's portable PDB, read the first time; null when it has none.</summary>
    public static DebugSymbols? EnsureDebugSymbols(LoadedModule module)
    {
        return GetLibrary(module).DebugSymbols.Value;
    }

    /// <summary>The read of the assembly's type system: begun by whoever asks first (the frame edge, reading it ahead of
    /// its first use, or that use), on the thread pool, and kept, so every asker takes the same outcome: the type system,
    /// or what the library threw on the module. A unit whose sync read needs it awaits this before that read starts,
    /// matches it once and hands its sync code the type system. Only <paramref name="ct"/> being cancelled before the
    /// read began ends it early. The module is added to <paramref name="reading"/>, the unit's, first: one closed before
    /// answers <see cref="TypeSystemRead.Closed"/>, read nothing. So is every module its type system was handed, the type
    /// system read again without one closed since.</summary>
    public static async Task<TypeSystemRead> ReadTypeSystemAsync(ModuleReading reading, LoadedModule module, CancellationToken ct)
    {
        if (!TryRead(reading, module))
        {
            return new TypeSystemRead.Closed { Module = module };
        }

        return await GetLibrary(module).TypeSystem.ReadAsync(reading, ct);
    }

    /// <summary>Reads <paramref name="module"/>'s type system ahead of its first use (the frame edge's warm-up), as a
    /// unit of its own; one released meanwhile is passed over. What it came to is that first use's to match.</summary>
    public static async Task<TypeSystemRead> ReadTypeSystemAheadAsync(LoadedModule module, CancellationToken ct)
    {
        using var reading = BeginReading();
        return await ReadTypeSystemAsync(reading, module, ct);
    }

    /// <summary>Awaits the read of <paramref name="module"/>'s type system, then does <paramref name="work"/> with it on
    /// the thread the read ends on, under a reading of its own, which it is handed; for a module the library could not
    /// read, says why and does nothing; for one closed before, says so and does nothing.</summary>
    internal static async Task<TypeSystemWork<T>> WithTypeSystemAsync<T>(LoadedModule module, Func<ModuleReading, DecompilerTypeSystem, T> work,
        CancellationToken ct)
    {
        using var reading = BeginReading();
        var read = await ReadTypeSystemAsync(reading, module, ct);
        return read.Match<TypeSystemWork<T>>(
            r => new TypeSystemWork<T>.Done { Value = work(reading, r.TypeSystem) },
            u => new TypeSystemWork<T>.Unreadable { Module = new UnreadableModule { Module = module, Why = u.Thrown.SourceException } },
            c => new TypeSystemWork<T>.Closed { Module = c.Module });
    }

    /// <summary>The reads of <paramref name="modules"/>' type systems, each added to <paramref name="reading"/>, the
    /// unit's, and awaited and matched once: those read, and those the library could not read, with what it threw, in
    /// the order given. One released before is passed over, as one no longer listed.</summary>
    internal static async Task<ModuleTypeSystems> ReadTypeSystemsAsync(ModuleReading reading, IEnumerable<LoadedModule> modules, CancellationToken ct)
    {
        var asked = new List<LoadedModule>();
        var read = new Dictionary<MetadataFile, DecompilerTypeSystem>();
        var unreadable = new List<UnreadableModule>();
        foreach (var module in modules)
        {
            var outcome = await ReadTypeSystemAsync(reading, module, ct);
            var typeSystem = outcome.Match<DecompilerTypeSystem?>(static r => r.TypeSystem, u => NoteUnreadable(unreadable, module, u), static _ => null);
            if (outcome is not TypeSystemRead.Closed)
            {
                asked.Add(module);
            }

            if (typeSystem is not null)
            {
                read[GetFile(module)] = typeSystem;
            }
        }
        return new ModuleTypeSystems { Modules = asked, Read = read, Unreadable = unreadable };
    }

    private static DecompilerTypeSystem? NoteUnreadable(List<UnreadableModule> unreadable, LoadedModule module, TypeSystemRead.Unreadable read)
    {
        unreadable.Add(new UnreadableModule { Module = module, Why = read.Thrown.SourceException });
        return null;
    }

    /// <summary>The type system decompiling with <paramref name="settings"/> reads: <paramref name="typeSystem"/>, the
    /// assembly's own as read, unless they turn language features on or off that it models; then the one made for
    /// them, made the first time on this thread, inside the caller's read. Every module it was handed is added to
    /// <paramref name="reading"/>, the unit's: with one closed since, it is made again, resolving without it.</summary>
    internal static DecompilerTypeSystem ChooseTypeSystemFor(ModuleReading reading, LoadedModule module, DecompilerTypeSystem typeSystem,
        DecompilerSettings settings)
    {
        var options = DecompilerTypeSystem.GetOptions(settings);
        if (options == DefaultTypeSystemOptions)
        {
            return typeSystem;
        }

        var library = GetLibrary(module);
        while (true)
        {
            var kept = library.TypeSystems.GetOrAdd(options, static (o, s) => new Lazy<TypeSystemBuild<DecompilerTypeSystem>>(() => BuildTypeSystem(s.library, s.reading, o)),
                (library, reading));
            var build = kept.Value;
            if (build.TryReadHandedOut(reading))
            {
                return build.Built;
            }

            library.TypeSystems.TryRemove(KeyValuePair.Create(options, kept));
        }
    }

    /// <summary>A type system of <paramref name="library"/>'s module with <paramref name="options"/>, what its resolver
    /// hands out added to <paramref name="reading"/>, the unit's, and recorded.</summary>
    private static TypeSystemBuild<DecompilerTypeSystem> BuildTypeSystem(ModuleLibrary library, ModuleReading reading, TypeSystemOptions options)
    {
        var resolver = new ReadingResolver(library, reading);
        var typeSystem = new DecompilerTypeSystem(library.File, resolver, options);
        return new TypeSystemBuild<DecompilerTypeSystem> { Built = typeSystem, HandedOut = resolver.ListHandedOut() };
    }

    /// <summary>What the assembly uses of the assemblies it references. Read the first time, then kept.</summary>
    public static ReferenceUsage EnsureReferenceUsage(LoadedModule module)
    {
        return GetLibrary(module).ReferenceUsage.Value;
    }

    /// <summary>The assemblies read out of the package. Read the first time, then kept.</summary>
    public static IReadOnlyList<LoadedModule> EnsureAssemblies(LoadedPackage package)
    {
        return package.Assemblies.Value;
    }

    /// <summary>The type or member <paramref name="main"/> defines that <paramref name="idString"/> names,
    /// written as XML documentation ids are; null when it defines none such, or the id cannot be read.</summary>
    internal static IEntity? FindDefinedEntity(MetadataModule main, string idString)
    {
        var entity = FindEntity(main, idString);
        if (entity is null)
        {
            return null;
        }

        if (!ReferenceEquals(entity.ParentModule, main))
        {
            return null;
        }

        return entity;
    }

    private static IEntity? FindEntity(MetadataModule main, string idString)
    {
        try
        {
            return IdStringProvider.FindEntity(idString, new SimpleTypeResolveContext(main));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (ReflectionNameParseException)
        {
            return null;
        }
    }

    /// <summary>The entity whose node holds the node of <paramref name="handle"/>'s: a property or
    /// event for its accessor, else the declaring type; null for a top-level type.</summary>
    internal static EntityHandle? FindOwner(MetadataModule main, EntityHandle handle)
    {
        return main.ResolveEntity(handle) switch
        {
            IMethod { IsAccessor: true } method when method.AccessorOwner is not null => method.AccessorOwner.MetadataToken,
            IEntity entity when entity.DeclaringTypeDefinition is not null => entity.DeclaringTypeDefinition.MetadataToken,
            _ => null,
        };
    }

    private static IReadOnlyList<LoadedModule> ListAssemblies(LoadedPackage package)
    {
        var assemblies = new List<LoadedModule>();
        foreach (var file in AssemblyPackages.ListFiles(package.Package))
        {
            if (!PackageContents.IsAssemblyName(file.Name))
            {
                continue;
            }

            var assembly = AssemblyListing.TryLoadOutOf(package.List, package, file);
            if (assembly is not null)
            {
                assemblies.Add(assembly);
            }
        }
        return assemblies;
    }

    /// <summary>The assembly of <paramref name="assemblies"/>, read out of <paramref name="package"/>, that
    /// came out of <paramref name="file"/>; null when that file did not load as one.</summary>
    internal static LoadedModule? FindAssembly(AssemblyPackage package, IReadOnlyList<LoadedModule> assemblies, PackageFile file)
    {
        var path = AssemblyPackages.GetFilePath(package, file);
        foreach (var assembly in assemblies)
        {
            if (assembly.FilePath.Equals(path))
            {
                return assembly;
            }
        }
        return null;
    }
}
