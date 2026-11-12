using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using static ILSight.Core.DebugSymbols;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.DebugInfo;

namespace ILSight.Core;

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

    /// <summary>Finding the portable PDB of an assembly, and checking one picked for it.</summary>
    private static class DebugSymbolFiles
    {
        private const string EmbeddedTitle = "Debug Metadata (Embedded)";
        private const string BesideTitle = "Debug Metadata (From portable PDB)";
        private const string EmbeddedDescription = "Embedded in this assembly";
        private const string BesideDescription = "Loaded from portable PDB: ";
        private const string WindowsPdbSignature = "Microsoft C/C++ MSF 7.00";

        /// <summary>Whether a portable PDB can be generated for <paramref name="file"/>: only when its debug
        /// directory names a PDB, which the generated one must match.</summary>
        internal static bool CanGenerateFor(PEFile file)
        {
            return PortablePdbWriter.HasCodeViewDebugDirectoryEntry(file);
        }

        /// <summary>The symbols for <paramref name="assembly"/>, or null when it has none or they cannot be
        /// read. A PDB beside it is looked for in <paramref name="package"/> when it was read out of one.</summary>
        public static DebugSymbols? FindSymbols(PEFile assembly, AssemblyPackage? package)
        {
            try
            {
                var entries = assembly.Reader.ReadDebugDirectory();
                var embedded = FindEmbedded(assembly, entries);
                if (embedded is not null)
                {
                    return embedded;
                }

                return FindBeside(assembly, package, entries);
            }
            catch (Exception e) when (IsUnreadable(e))
            {
                return null;
            }
        }

        /// <summary>The symbols in the portable PDB at <paramref name="pdbPath"/>, picked for the assembly;
        /// null when it cannot be read.</summary>
        public static DebugSymbols? ReadPicked(string pdbPath)
        {
            try
            {
                var image = System.IO.File.ReadAllBytes(pdbPath);
                var provider = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(image));
                var file = new MetadataFile(MetadataFile.MetadataFileKind.ProgramDebugDatabase, pdbPath, provider);
                return new DebugSymbols { File = file, Title = BesideTitle, Description = BesideDescription + pdbPath };
            }
            catch (Exception e) when (IsUnreadable(e))
            {
                return null;
            }
        }

        /// <summary>Whether the PDB at <paramref name="pdbPath"/> can be read for <paramref name="assembly"/>.</summary>
        public static PdbCheck CheckPdb(PEFile assembly, string pdbPath)
        {
            try
            {
                var image = System.IO.File.ReadAllBytes(pdbPath);
                if (IsWindowsPdb(image))
                {
                    return PdbCheck.WindowsPdb;
                }

                var provider = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(image));
                var pdbReader = provider.GetMetadataReader();
                if (pdbReader.DebugMetadataHeader is null)
                {
                    return PdbCheck.Unreadable;
                }

                return CheckBuild(assembly, provider);
            }
            catch (Exception e) when (IsUnreadable(e))
            {
                return PdbCheck.Unreadable;
            }
        }

        private static bool IsWindowsPdb(byte[] image)
        {
            var signature = System.Text.Encoding.ASCII.GetBytes(WindowsPdbSignature);
            return image.AsSpan().StartsWith(signature);
        }

        /// <summary>Whether the PDB is of the build one of the assembly's CodeView entries names; any
        /// PDB matches an assembly that names none.</summary>
        private static PdbCheck CheckBuild(PEFile assembly, MetadataReaderProvider provider)
        {
            var namesBuild = false;
            foreach (var entry in assembly.Reader.ReadDebugDirectory())
            {
                if (entry.Type != DebugDirectoryEntryType.CodeView)
                {
                    continue;
                }

                namesBuild = true;
                var codeView = assembly.Reader.ReadCodeViewDebugDirectoryData(entry);
                if (IsBuiltWith(provider, codeView, entry))
                {
                    return PdbCheck.Matches;
                }
            }
            if (namesBuild)
            {
                return PdbCheck.OtherBuild;
            }

            return PdbCheck.Matches;
        }

        private static DebugSymbols? FindEmbedded(PEFile assembly, ImmutableArray<DebugDirectoryEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.Type != DebugDirectoryEntryType.EmbeddedPortablePdb)
                {
                    continue;
                }

                var provider = assembly.Reader.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
                var file = new MetadataFile(MetadataFile.MetadataFileKind.ProgramDebugDatabase, assembly.FileName, provider, isEmbedded: true);
                return new DebugSymbols { File = file, Title = EmbeddedTitle, Description = EmbeddedDescription };
            }
            return null;
        }

        private static DebugSymbols? FindBeside(PEFile assembly, AssemblyPackage? package, ImmutableArray<DebugDirectoryEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.Type != DebugDirectoryEntryType.CodeView)
                {
                    continue;
                }

                var codeView = assembly.Reader.ReadCodeViewDebugDirectoryData(entry);
                var pdb = ReadBeside(assembly.FileName, codeView.Path, package);
                if (pdb is null)
                {
                    continue;
                }

                var provider = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(pdb.Image));
                if (!IsBuiltWith(provider, codeView, entry))
                {
                    continue;
                }

                var file = new MetadataFile(MetadataFile.MetadataFileKind.ProgramDebugDatabase, pdb.Path, provider);
                return new DebugSymbols { File = file, Title = BesideTitle, Description = BesideDescription + pdb.Path };
            }
            return null;
        }

        /// <summary>The PDB the CodeView entry names in the assembly's folder, else the one named
        /// after the assembly; null when neither is there.</summary>
        private static PdbImage? ReadBeside(string assemblyPath, string codeViewPath, AssemblyPackage? package)
        {
            var folder = Path.GetDirectoryName(assemblyPath) ?? "";
            var named = Path.Combine(folder, Path.GetFileName(codeViewPath));
            var image = TryReadFile(named, package);
            if (image is not null)
            {
                return new PdbImage(named, image);
            }

            var sibling = Path.ChangeExtension(assemblyPath, DebugSymbols.PdbExtension);
            image = TryReadFile(sibling, package);
            if (image is not null)
            {
                return new PdbImage(sibling, image);
            }

            return null;
        }

        /// <summary>The bytes of the file at <paramref name="path"/>: on disk, or in <paramref name="package"/>
        /// when there is one, where the path runs through it. Null when there is no such file.</summary>
        private static byte[]? TryReadFile(string path, AssemblyPackage? package)
        {
            if (package is null)
            {
                return TryReadFromDisk(path);
            }

            var inPackage = AssemblyPath.Parse(path);
            if (inPackage is null)
            {
                return null;
            }

            var file = AssemblyPackages.FindFileAt(package, inPackage);
            if (file is null)
            {
                return null;
            }

            return AssemblyPackages.ReadFile(package, file);
        }

        private static byte[]? TryReadFromDisk(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                return null;
            }

            return System.IO.File.ReadAllBytes(path);
        }

        private static bool IsBuiltWith(MetadataReaderProvider provider, CodeViewDebugDirectoryData codeView, DebugDirectoryEntry entry)
        {
            var header = provider.GetMetadataReader().DebugMetadataHeader;
            if (header is null)
            {
                return false;
            }

            var id = new BlobContentId(header.Id);
            if (id.Guid != codeView.Guid)
            {
                return false;
            }

            return id.Stamp == entry.Stamp;
        }

        private static bool IsUnreadable(Exception e)
        {
            return e is BadImageFormatException or IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException;
        }
    }
}
