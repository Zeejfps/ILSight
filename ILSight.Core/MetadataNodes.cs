using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;
using ILSight.Core.MetadataArea;

namespace ILSight.Core;

internal static class MetadataNodes
{
    /// <summary>The node of the metadata of <paramref name="file"/>, titled <paramref name="title"/>, under <paramref name="parent"/>.</summary>
    internal static BrowseNode CreateMetadataFolder(LoadedModule module, BrowseNode parent, MetadataFile file, string title)
    {
        return BrowseNodes.CreateListed(parent, new NodeSubject.Metadata { Value = new MetadataFolderInfo { Module = module, File = file, Title = title } });
    }

    private static BrowseNode CreatePart(BrowseNode parent, MetadataTableInfo part)
    {
        return BrowseNodes.CreateListed(parent, new NodeSubject.MetadataTable { Value = part });
    }

    /// <summary>For a PE file its headers and debug directory, then the tables and the four heaps.</summary>
    internal static BrowseNode[] ListMetadata(BrowseNode parent, MetadataFolderInfo folder)
    {
        var (headers, heaps) = ListingReads.ValueOf(ReadParts(folder));
        var result = headers.Select(p => CreatePart(parent, p)).ToList();
        result.Add(BrowseNodes.CreateListed(parent, new NodeSubject.MetadataTables { Folder = folder }));
        result.AddRange(heaps.Select(p => CreatePart(parent, p)));
        return result.ToArray();
    }

    // The parts the folder lists before its tables (for a PE file its headers, data directories and debug directory),
    // and after them (the four heaps), each read from the file.
    private static Outcome<(MetadataTableInfo[] Headers, MetadataTableInfo[] Heaps)> ReadParts(MetadataFolderInfo folder)
    {
        try
        {
            var file = folder.File;
            var headers = new List<MetadataTableInfo>();
            if (file is PEFile pe)
            {
                headers.Add(CreateHeader(folder, PEHeaderKind.Dos, PEHeaderEntries.Dos(pe)));
                headers.Add(CreateHeader(folder, PEHeaderKind.Coff, PEHeaderEntries.Coff(pe)));
                headers.Add(CreateHeader(folder, PEHeaderKind.Optional, PEHeaderEntries.Optional(pe)));
                var directories = PEHeaderEntries.DataDirectories(pe);
                headers.Add(MetadataTables.CreateGridPart(folder.Module, file, directories.Key, new MetadataPartKey.DataDirectories(), MetadataGridRole.Directory, directories));
                headers.Add(MetadataTables.CreateDebugDirectoryPart(folder.Module, pe));
            }

            var heaps = MetadataHeaps.Create(file)
                .Select(h => MetadataTables.CreateGridPart(folder.Module, file, h.Title, new MetadataPartKey.Heap(h.Schema.Key), MetadataGridRole.Heap, h.Schema))
                .ToArray();
            return new Outcome<(MetadataTableInfo[] Headers, MetadataTableInfo[] Heaps)>.Ok((headers.ToArray(), heaps));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(MetadataTableInfo[] Headers, MetadataTableInfo[] Heaps)>.Failed(e);
        }
    }

    private static MetadataTableInfo CreateHeader(MetadataFolderInfo folder, PEHeaderKind which, GridSchema header)
    {
        return MetadataTables.CreateGridPart(folder.Module, folder.File, header.Key, new MetadataPartKey.Header(which), MetadataGridRole.Header, header);
    }

    /// <summary>The entries of the debug directory: what ILSpy decodes of each, else its raw bytes; the
    /// embedded PDB as the metadata folder of the debug metadata the module already loaded.</summary>
    internal static BrowseNode[] ListDebugEntries(BrowseNode parent, LoadedModule module, PEFile file)
    {
        var reader = file.Reader;
        var result = new List<BrowseNode>();
        var entries = ListingReads.ValueOf(ReadDebugDirectory(reader));
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var (decoded, failed) = ListingReads.ValueOf(ReadDecodedOrRaw(reader, module, entry));

            // An embedded PDB the module has none of shows nothing, as before; the lines are made of what was read,
            // outside the read's catch.
            if (decoded is null && entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb && !failed)
            {
                continue;
            }

            result.Add(decoded switch
            {
                CodeViewDebugDirectoryData codeView => CreatePart(parent, MetadataTables.CreateDecodedEntryPart(module, file, index, entry,
                    [$"GUID: {codeView.Guid}", $"Age: {codeView.Age}", $"Path: {codeView.Path}"])),
                PdbChecksumDebugDirectoryData checksum => CreatePart(parent, MetadataTables.CreateDecodedEntryPart(module, file, index, entry,
                    [$"AlgorithmName: {checksum.AlgorithmName}", $"Checksum: {Convert.ToHexStringLower(checksum.Checksum.AsSpan())}"])),
                DebugSymbols symbols => CreateMetadataFolder(module, parent, symbols.File, symbols.Title),
                _ => CreatePart(parent, MetadataTables.CreateRawEntryPart(module, file, index, entry)),
            });
        }
        return result.ToArray();
    }

    private static Outcome<ImmutableArray<DebugDirectoryEntry>> ReadDebugDirectory(PEReader reader)
    {
        try
        {
            return new Outcome<ImmutableArray<DebugDirectoryEntry>>.Ok(reader.ReadDebugDirectory());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<ImmutableArray<DebugDirectoryEntry>>.Failed(e);
        }
    }

    // What the library decodes of the entry, as ReadDecoded; an entry it can't decode is shown raw, as one of a type ILSpy
    // doesn't decode, and said failed.
    private static Outcome<(object? Decoded, bool Failed)> ReadDecodedOrRaw(PEReader reader, LoadedModule module, DebugDirectoryEntry entry)
    {
        try
        {
            return new Outcome<(object? Decoded, bool Failed)>.Ok((ReadDecoded(reader, module, entry), false));
        }
        catch (BadImageFormatException)
        {
            return new Outcome<(object? Decoded, bool Failed)>.Ok((null, true));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(object? Decoded, bool Failed)>.Failed(e);
        }
    }

    // What the library decodes of the entry: its CodeView data, its PDB checksum, the embedded PDB's symbols; null for an
    // entry shown raw (a type not decoded, or an embedded PDB the module has none of).
    private static object? ReadDecoded(PEReader reader, LoadedModule module, DebugDirectoryEntry entry)
    {
        return entry.Type switch
        {
            DebugDirectoryEntryType.CodeView => reader.ReadCodeViewDebugDirectoryData(entry),
            DebugDirectoryEntryType.PdbChecksum => reader.ReadPdbChecksumDebugDirectoryData(entry),
            DebugDirectoryEntryType.EmbeddedPortablePdb => FindEmbeddedSymbols(module),
            _ => null,
        };
    }

    private static DebugSymbols? FindEmbeddedSymbols(LoadedModule module)
    {
        var symbols = LoadedAssemblies.EnsureDebugSymbols(module);
        if (symbols is null)
        {
            return null;
        }

        if (!symbols.File.IsEmbedded)
        {
            return null;
        }

        return symbols;
    }
}
