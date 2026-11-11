using static ILSight.Core.DebugSymbols;
// Adapted from ILSpy's PortableDebugInfoProvider and DebugInfoUtils (MIT License, Copyright (c) AlphaSierraPapa for the SharpDevelop Team).

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Metadata;
using SequencePoint = ICSharpCode.Decompiler.DebugInfo.SequencePoint;

namespace ILSight.Core;

/// <summary>Whether a PDB file picked for an assembly can be read for it.</summary>
public enum PdbCheck
{
    /// <summary>It is a portable PDB of this build of the assembly, or one the assembly names no build for.</summary>
    Matches,

    /// <summary>It is a portable PDB of another build of the assembly.</summary>
    OtherBuild,

    /// <summary>It is a Windows PDB, which cannot be read.</summary>
    WindowsPdb,

    /// <summary>It is no PDB, or it cannot be read.</summary>
    Unreadable,
}

/// <summary>
/// The portable PDB that goes with an assembly: the one picked for it, else the one embedded in it,
/// else the one next to it whose id matches its CodeView entry; next to it in the package, for an
/// assembly read out of one. The decompiler takes local names from it, and the tree shows its
/// metadata beside the assembly's.
/// </summary>
/// <remarks>The decompiler's IDebugInfoProvider demands the methods here, which forward to <see cref="DebugSymbolFiles"/>.</remarks>
public sealed class DebugSymbols : IDebugInfoProvider
{
    internal const string PdbExtension = ".pdb";

    /// <summary>The PDB's own metadata.</summary>
    public required MetadataFile File { get; init; }

    /// <summary>What the tree calls <see cref="File"/>.</summary>
    public required string Title { get; init; }

    public required string Description { get; init; }

    public string SourceFileName => File.FileName;

    public MetadataReader Metadata => File.Metadata;

    public sealed record PdbImage(string Path, byte[] Image);

    public IList<SequencePoint> GetSequencePoints(MethodDefinitionHandle method)
    {
        return DebugSymbolFiles.GetSequencePoints(this, method);
    }

    public IList<Variable> GetVariables(MethodDefinitionHandle method)
    {
        return DebugSymbolFiles.GetVariables(this, method);
    }

    public bool TryGetName(MethodDefinitionHandle method, int index, [MaybeNullWhen(false)] out string name)
    {
        return DebugSymbolFiles.TryGetName(this, method, index, out name);
    }

    /// <summary>The tuple element names and <c>dynamic</c> flags the compiler recorded for local
    /// <paramref name="index"/>, which its signature cannot carry.</summary>
    public bool TryGetExtraTypeInfo(MethodDefinitionHandle method, int index, out PdbExtraTypeInfo extraTypeInfo)
    {
        return DebugSymbolFiles.TryGetExtraTypeInfo(this, method, index, out extraTypeInfo);
    }
}

/// <summary>Finding the portable PDB of an assembly, and reading the names and lines it holds.</summary>
internal static class DebugSymbolFiles
{
    private const string EmbeddedTitle = "Debug Metadata (Embedded)";
    private const string BesideTitle = "Debug Metadata (From portable PDB)";
    private const string EmbeddedDescription = "Embedded in this assembly";
    private const string BesideDescription = "Loaded from portable PDB: ";
    private const string WindowsPdbSignature = "Microsoft C/C++ MSF 7.00";
    private const int BitsPerByte = 8;

    /// <summary>Whether a portable PDB can be generated for <paramref name="file"/>: only when its debug
    /// directory names a PDB, which the generated one must match.</summary>
    internal static bool CanGenerateFor(PEFile file)
    {
        return PortablePdbWriter.HasCodeViewDebugDirectoryEntry(file);
    }

    /// <summary>The symbols for <paramref name="assembly"/>, or null when it has none or they cannot be
    /// read. A PDB beside it is looked for in <paramref name="package"/> when it was read out of one.</summary>
    public static DebugSymbols? LoadSymbols(PEFile assembly, AssemblyPackage? package)
    {
        try
        {
            var entries = assembly.Reader.ReadDebugDirectory();
            var embedded = LoadEmbedded(assembly, entries);
            if (embedded is not null)
            {
                return embedded;
            }

            return LoadBeside(assembly, package, entries);
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            return null;
        }
    }

    /// <summary>The symbols in the portable PDB at <paramref name="pdbPath"/>, picked for the assembly;
    /// null when it cannot be read.</summary>
    public static DebugSymbols? LoadPicked(string pdbPath)
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
            if (provider.GetMetadataReader().DebugMetadataHeader is null)
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

    private static DebugSymbols? LoadEmbedded(PEFile assembly, ImmutableArray<DebugDirectoryEntry> entries)
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

    private static DebugSymbols? LoadBeside(PEFile assembly, AssemblyPackage? package, ImmutableArray<DebugDirectoryEntry> entries)
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

    public static IList<SequencePoint> GetSequencePoints(DebugSymbols symbols, MethodDefinitionHandle method)
    {
        var points = new List<SequencePoint>();
        var debugInformation = symbols.Metadata.GetMethodDebugInformation(method);
        foreach (var point in debugInformation.GetSequencePoints())
        {
            points.Add(ToSequencePoint(symbols, point));
        }

        return points;
    }

    private static SequencePoint ToSequencePoint(DebugSymbols symbols, System.Reflection.Metadata.SequencePoint point)
    {
        return new SequencePoint
        {
            Offset = point.Offset,
            StartLine = point.StartLine,
            StartColumn = point.StartColumn,
            EndLine = point.EndLine,
            EndColumn = point.EndColumn,
            DocumentUrl = GetDocumentName(symbols, point.Document),
        };
    }

    private static string GetDocumentName(DebugSymbols symbols, DocumentHandle handle)
    {
        if (handle.IsNil)
        {
            return "";
        }

        var document = symbols.Metadata.GetDocument(handle);
        return symbols.Metadata.GetString(document.Name);
    }

    public static IList<Variable> GetVariables(DebugSymbols symbols, MethodDefinitionHandle method)
    {
        var variables = new List<Variable>();
        foreach (var handle in ListLocals(symbols, method))
        {
            var local = symbols.Metadata.GetLocalVariable(handle);
            variables.Add(new Variable(local.Index, symbols.Metadata.GetString(local.Name)));
        }
        return variables;
    }

    public static bool TryGetName(DebugSymbols symbols, MethodDefinitionHandle method, int index, [MaybeNullWhen(false)] out string name)
    {
        name = null;
        var handle = FindLocal(symbols, method, index);
        if (handle.IsNil)
        {
            return false;
        }

        var local = symbols.Metadata.GetLocalVariable(handle);
        name = symbols.Metadata.GetString(local.Name);
        return true;
    }

    /// <summary>The tuple element names and <c>dynamic</c> flags the compiler recorded for local
    /// <paramref name="index"/>, which its signature cannot carry.</summary>
    public static bool TryGetExtraTypeInfo(DebugSymbols symbols, MethodDefinitionHandle method, int index, out PdbExtraTypeInfo extraTypeInfo)
    {
        extraTypeInfo = default;
        var local = FindLocal(symbols, method, index);
        if (local.IsNil)
        {
            return false;
        }

        foreach (var handle in symbols.Metadata.GetCustomDebugInformation(local))
        {
            var information = symbols.Metadata.GetCustomDebugInformation(handle);
            var kind = symbols.Metadata.GetGuid(information.Kind);
            if (kind == KnownGuids.TupleElementNames)
            {
                extraTypeInfo.TupleElementNames = ReadTupleElementNames(symbols, information.Value);
            }
            else if (kind == KnownGuids.DynamicLocalVariables)
            {
                extraTypeInfo.DynamicFlags = ReadDynamicFlags(symbols, information.Value);
            }
        }
        if (extraTypeInfo.TupleElementNames is not null)
        {
            return true;
        }

        return extraTypeInfo.DynamicFlags is not null;
    }

    private static LocalVariableHandle FindLocal(DebugSymbols symbols, MethodDefinitionHandle method, int index)
    {
        foreach (var handle in ListLocals(symbols, method))
        {
            if (symbols.Metadata.GetLocalVariable(handle).Index == index)
            {
                return handle;
            }
        }
        return default;
    }

    private static IEnumerable<LocalVariableHandle> ListLocals(DebugSymbols symbols, MethodDefinitionHandle method)
    {
        foreach (var scopeHandle in symbols.Metadata.GetLocalScopes(method))
        {
            var scope = symbols.Metadata.GetLocalScope(scopeHandle);
            foreach (var local in scope.GetLocalVariables())
            {
                yield return local;
            }
        }
    }

    private static string?[] ReadTupleElementNames(DebugSymbols symbols, BlobHandle blob)
    {
        var reader = symbols.Metadata.GetBlobReader(blob);
        var names = new List<string?>();
        while (reader.RemainingBytes > 0)
        {
            names.Add(ReadTupleElementName(ref reader));
        }

        return names.ToArray();
    }

    /// <summary>A null-terminated UTF-8 name; null for an element left unnamed.</summary>
    private static string? ReadTupleElementName(ref BlobReader reader)
    {
        var length = reader.IndexOf(0);
        var name = reader.ReadUTF8(length);
        reader.ReadByte();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return name;
    }

    /// <summary>One flag per bit, lowest bit of each byte first.</summary>
    private static bool[] ReadDynamicFlags(DebugSymbols symbols, BlobHandle blob)
    {
        var bytes = symbols.Metadata.GetBlobBytes(blob);
        var flags = new bool[bytes.Length * BitsPerByte];
        for (var i = 0; i < flags.Length; i++)
        {
            var bit = 1 << (i % BitsPerByte);
            flags[i] = (bytes[i / BitsPerByte] & bit) != 0;
        }
        return flags;
    }
}
