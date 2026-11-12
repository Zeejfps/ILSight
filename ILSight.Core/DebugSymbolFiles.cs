using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>Finding the portable PDB of an assembly, and checking one picked for it.</summary>
internal static class DebugSymbolFiles
{
    private const string EmbeddedTitle = "Debug Metadata (Embedded)";
    private const string BesideTitle = "Debug Metadata (From portable PDB)";
    private const string EmbeddedDescription = "Embedded in this assembly";
    private const string BesideDescription = "Loaded from portable PDB: ";
    private const string WindowsPdbSignature = "Microsoft C/C++ MSF 7.00";

    private sealed record PdbImage(string Path, byte[] Image);

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
