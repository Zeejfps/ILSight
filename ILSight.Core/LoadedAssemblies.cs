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
    /// does.</summary>
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
        };
        Libraries.Add(module, new ModuleLibrary
        {
            File = file,
            Resolver = resolver,
            TypeSystem = new Lazy<DecompilerTypeSystem>(() => new DecompilerTypeSystem(file, resolver, Settings.Create())),
            DebugSymbols = new Lazy<DebugSymbols?>(() => FindDebugSymbols(list, path, file, place)),
            ReferenceUsage = new Lazy<ReferenceUsage>(() => ForwardedTypes.ReadUsage(file.Metadata)),
        });
        return module;
    }

    /// <summary>The library's objects kept against <paramref name="module"/>; throws for one not made here.</summary>
    private static ModuleLibrary GetLibrary(LoadedModule module)
    {
        if (!Libraries.TryGetValue(module, out var library))
        {
            throw new InvalidOperationException($"The module '{module.FilePath}' was not read by {nameof(LoadedAssemblies)}.");
        }

        return library;
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

    /// <summary>The resolver of what <paramref name="module"/> references.</summary>
    internal static AssemblyResolver GetResolver(LoadedModule module)
    {
        return GetLibrary(module).Resolver;
    }

    /// <summary>Whether <paramref name="module"/> was read from <paramref name="file"/>, the very one.</summary>
    internal static bool IsReadFrom(LoadedModule module, MetadataFile file)
    {
        return ReferenceEquals(GetFile(module), file);
    }

    /// <summary>Whether <paramref name="folder"/> shows the metadata of its module's own file, rather than that of its
    /// debug symbols.</summary>
    internal static bool IsOwnMetadata(MetadataFolderInfo folder)
    {
        return ReferenceEquals(folder.File, GetFile(folder.Module));
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


    /// <summary>The assembly's portable PDB, read the first time; null when it has none.</summary>
    public static DebugSymbols? EnsureDebugSymbols(LoadedModule module)
    {
        return GetLibrary(module).DebugSymbols.Value;
    }

    /// <summary>The assembly's type system, read the first time.</summary>
    public static DecompilerTypeSystem EnsureTypeSystem(LoadedModule module)
    {
        return GetLibrary(module).TypeSystem.Value;
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

        var library = GetLibrary(module);
        return library.TypeSystems.GetOrAdd(options, static (o, l) => new Lazy<DecompilerTypeSystem>(() => new DecompilerTypeSystem(l.File, l.Resolver, o)), library).Value;
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
