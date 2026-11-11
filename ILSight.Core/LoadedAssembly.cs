using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Where an assembly read out of a package came from: the package, as listed, and its file there.</summary>
public sealed record PackagePlace(LoadedPackage Container, PackageFile File);

/// <summary>A folder of a listed package, whose files can be extracted: the package and the folder.</summary>
public sealed record PackageFolderPlace(LoadedPackage Container, PackageFolder Folder);

/// <summary>
/// A file known to an <see cref="AssemblyList"/>: one the user opened, or one loaded to resolve a
/// reference. It is a <see cref="LoadedModule"/> that was read, a <see cref="FailedAssembly"/> that
/// could not be, or a <see cref="LoadedPackage"/> whose files are browsed; no other kind can be made.
/// Made by <see cref="LoadedAssemblies"/>, whose functions work out what these hold from the file.
/// A <see cref="BrowseSession"/> keeps the nodes of its tree for as long as this object lives.
/// </summary>
public abstract class LoadedAssembly
{
    private protected LoadedAssembly()
    {
    }

    /// <summary>The absolute path of the file this was loaded from; for an assembly read out of a
    /// package, the path through the package, as <see cref="AssemblyPackages.GetFilePath"/> gives it.</summary>
    public required AssemblyPath FilePath { get; init; }

    /// <summary>The item of the list that holds this assembly: itself, or the package it was read out of.</summary>
    public LoadedAssembly ListedItem
    {
        get
        {
            if (this is LoadedModule module && module.Place is not null)
            {
                return module.Place.Container;
            }

            return this;
        }
    }

    /// <summary>The simple name, e.g. <c>System.Private.CoreLib</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The full display name, with version, culture and public key token.</summary>
    public required string FullName { get; init; }

    public required Version? Version { get; init; }

    /// <summary>The simple name with the version, e.g. <c>System.Runtime (10.0.0.0)</c>.</summary>
    public required string NameAndVersion { get; init; }

    public required AssemblyList List { get; init; }

    public override string ToString()
    {
        return FullName;
    }
}

/// <summary>An assembly or WebCIL module that was read: a file on disk, or, when <see cref="Place"/>
/// says so, one read out of a package, known by the path <see cref="AssemblyPackages.GetFilePath"/> gives it.
/// What takes reading is held as a <see cref="Lazy{T}"/>, read once whichever thread asks first, and asked
/// for with the <c>Ensure</c> functions of <see cref="LoadedAssemblies"/>.</summary>
public sealed class LoadedModule : LoadedAssembly
{
    public required Lazy<DecompilerTypeSystem> TypeSystem { get; init; }

    /// <summary>The assembly's portable PDB; null when it has none.</summary>
    public required Lazy<DebugSymbols?> DebugSymbols { get; init; }

    /// <summary>Whether a PDB can be generated for this assembly: only one whose debug directory
    /// names a PDB, which the generated one must match.</summary>
    public required Lazy<bool> CanGeneratePdb { get; init; }

    /// <summary>What the assembly uses of the assemblies it references.</summary>
    public required Lazy<ReferenceUsage> ReferenceUsage { get; init; }

    /// <summary>The type systems of the settings that turn language features on or off that
    /// <see cref="TypeSystem"/> models.</summary>
    public readonly ConcurrentDictionary<TypeSystemOptions, Lazy<DecompilerTypeSystem>> TypeSystems = new();

    /// <summary>The package this was read out of, and its file there; null for a file on disk.</summary>
    public required PackagePlace? Place { get; init; }

    public required MetadataFile File { get; init; }

    /// <summary><see cref="File"/> as a portable executable; null for a WebCIL module, which has no PE headers.</summary>
    internal PEFile? PEFile => File as PEFile;

    public required AssemblyResolver Resolver { get; init; }

    /// <summary>The target framework the assembly was built for, e.g. <c>.NETCoreApp,Version=v10.0</c>,
    /// or null if it does not say.</summary>
    public required TargetFrameworkId? DetectedTargetFramework { get; init; }

    /// <summary>The framework the assembly's references are resolved against: the one the list picked
    /// for it, else <see cref="DetectedTargetFramework"/>.</summary>
    public required TargetFrameworkId? TargetFramework { get; init; }

    /// <summary>Whether the list picked <see cref="TargetFramework"/> for this assembly.</summary>
    public required bool IsTargetFrameworkPicked { get; init; }

    /// <summary><see cref="TargetFramework"/> as ILSpy shows it, e.g. <c>.NETCoreApp, v10.0</c>.</summary>
    public string? FrameworkName
    {
        get
        {
            if (TargetFramework is null)
            {
                return null;
            }

            return TargetFramework.Readable;
        }
    }

    /// <summary><see cref="TargetFramework"/> as a moniker, e.g. <c>net10.0</c>, <c>netstandard2.0</c>
    /// or <c>net48</c>.</summary>
    public string? FrameworkMoniker
    {
        get
        {
            if (TargetFramework is null)
            {
                return null;
            }

            return TargetFramework.Moniker;
        }
    }

    /// <summary>Whether a PDB can be picked for this assembly: one that loaded with PE headers, which
    /// a WebCIL module lacks.</summary>
    public bool CanPickPdb => PEFile is not null;
}

/// <summary>A file of the list that could not be read as a .NET assembly, kept listed to show why. It
/// has no metadata: only its <see cref="LoadedAssembly.FilePath"/>, <see cref="LoadedAssembly.Name"/>
/// and <see cref="Error"/> tell anything about it.</summary>
public sealed class FailedAssembly : LoadedAssembly
{
    /// <summary>Why the file could not be read.</summary>
    public required AssemblyLoadException Error { get; init; }
}

/// <summary>A package of the list, whose files are browsed as folders.</summary>
public sealed class LoadedPackage : LoadedAssembly
{
    /// <summary>The package's files.</summary>
    public required AssemblyPackage Package { get; init; }

    /// <summary>The assemblies read out of the package: every assembly file it holds that loads, in
    /// the order its tree shows them. Its reading needs the package it reads for, so
    /// <see cref="LoadedAssemblies.CreatePackage"/> assigns this just after making the package, and nothing
    /// sees the package before that; reading the value this starts with throws.</summary>
    public Lazy<IReadOnlyList<LoadedModule>> Assemblies { get; internal set; } = new(static () => throw new InvalidOperationException("A package is made by LoadedAssemblies.CreatePackage."));
}

/// <summary>Making what an <see cref="AssemblyList"/> holds, and asking it for what takes reading.</summary>
public static class LoadedAssemblies
{
    private static readonly TypeSystemOptions DefaultTypeSystemOptions = DecompilerTypeSystem.GetOptions(Settings.Create());

    /// <summary>The module <paramref name="file"/> holds, known by <paramref name="path"/>, with nothing
    /// but its headers read yet.</summary>
    internal static LoadedModule CreateModule(AssemblyList list, AssemblyPath path, MetadataFile file, PackagePlace? place = null)
    {
        var name = GetModuleName(path, file);
        var version = FindVersion(file);
        var detected = TryDetectTargetFramework(file);
        var picked = list.TargetFrameworks.Find(path);
        var targetFramework = picked ?? detected;
        var resolver = AssemblyResolver.Create(list, path, targetFramework, place?.Container.Package);
        return new LoadedModule
        {
            List = list,
            FilePath = path,
            Name = name,
            FullName = GetFullName(file),
            Version = version,
            NameAndVersion = JoinNameAndVersion(name, version),
            File = file,
            Place = place,
            DetectedTargetFramework = detected,
            TargetFramework = targetFramework,
            IsTargetFrameworkPicked = picked is not null,
            Resolver = resolver,
            TypeSystem = new Lazy<DecompilerTypeSystem>(() => new DecompilerTypeSystem(file, resolver, Settings.Create())),
            DebugSymbols = new Lazy<DebugSymbols?>(() => FindDebugSymbols(list, path, file, place)),
            CanGeneratePdb = new Lazy<bool>(() => file is PEFile pe && DebugSymbolFiles.CanGenerateFor(pe)),
            ReferenceUsage = new Lazy<ReferenceUsage>(() => ForwardedTypes.ReadUsage(file.Metadata)),
        };
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

    /// <summary>Whether the PDB at <paramref name="pdbPath"/> can be read for this assembly. Reads the file.</summary>
    public static PdbCheck CheckPdb(LoadedModule module, string pdbPath)
    {
        if (module.PEFile is null)
        {
            return PdbCheck.Unreadable;
        }

        return DebugSymbolFiles.CheckPdb(module.PEFile, pdbPath);
    }

    /// <summary>The name a portable PDB of this assembly is written under.</summary>
    public static string GetPdbFileName(LoadedAssembly assembly)
    {
        return WholeProjectDecompiler.CleanUpFileName(assembly.Name, DebugSymbols.PdbExtension);
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

    /// <summary>Whether a PDB can be generated for <paramref name="module"/>, worked out the first time.</summary>
    public static bool CanGeneratePdb(LoadedModule module)
    {
        return module.CanGeneratePdb.Value;
    }

    /// <summary>The assembly's portable PDB, read the first time; null when it has none.</summary>
    public static DebugSymbols? EnsureDebugSymbols(LoadedModule module)
    {
        return module.DebugSymbols.Value;
    }

    /// <summary>The assembly's type system, read the first time.</summary>
    public static DecompilerTypeSystem EnsureTypeSystem(LoadedModule module)
    {
        return module.TypeSystem.Value;
    }

    /// <summary>The module of the type system this assembly's metadata is read into, read the first time.</summary>
    public static MetadataModule EnsureMainModule(LoadedModule module)
    {
        return EnsureTypeSystem(module).MainModule;
    }

    /// <summary>The type system decompiling with <paramref name="settings"/> reads: the assembly's own
    /// unless they turn language features on or off that it models.</summary>
    internal static DecompilerTypeSystem EnsureTypeSystemFor(LoadedModule module, DecompilerSettings settings)
    {
        var options = DecompilerTypeSystem.GetOptions(settings);
        if (options == DefaultTypeSystemOptions)
        {
            return EnsureTypeSystem(module);
        }

        return module.TypeSystems.GetOrAdd(options, static (o, m) => new Lazy<DecompilerTypeSystem>(() => new DecompilerTypeSystem(m.File, m.Resolver, o)), module).Value;
    }

    /// <summary>What the assembly uses of the assemblies it references. Read the first time, then kept.</summary>
    public static ReferenceUsage EnsureReferenceUsage(LoadedModule module)
    {
        return module.ReferenceUsage.Value;
    }

    /// <summary>The assemblies read out of the package. Read the first time, then kept.</summary>
    public static IReadOnlyList<LoadedModule> EnsureAssemblies(LoadedPackage package)
    {
        return package.Assemblies.Value;
    }

    /// <summary>Reads the type system ahead of its first use, on a thread-pool thread. Whatever
    /// reading it throws is left for that first use, which reads it again and reports it; it is caught
    /// whole here because an exception leaving a thread-pool work item ends the process.</summary>
    internal static void WarmUp(LoadedModule module)
    {
        try
        {
            _ = EnsureTypeSystem(module);
        }
        catch (Exception)
        {
        }
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
        catch (Exception e) when (e is ArgumentException or ReflectionNameParseException)
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
