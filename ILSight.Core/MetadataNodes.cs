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
        return BrowseNodes.Create(parent, new NodeSubject.Metadata { Value = new MetadataFolderInfo { Module = module, File = file, Title = title } });
    }

    private static BrowseNode CreatePart(BrowseNode parent, MetadataTableInfo part)
    {
        return BrowseNodes.Create(parent, new NodeSubject.MetadataTable { Value = part });
    }

    /// <summary>For a PE file its headers and debug directory, then the tables and the four heaps.</summary>
    internal static BrowseNode[] ListMetadata(BrowseNode parent, MetadataFolderInfo folder)
    {
        var file = folder.File;
        var result = new List<BrowseNode>();
        if (file is PEFile pe)
        {
            AddHeader(parent, result, folder, PEHeaderKind.Dos, PEHeaderEntries.Dos(pe));
            AddHeader(parent, result, folder, PEHeaderKind.Coff, PEHeaderEntries.Coff(pe));
            AddHeader(parent, result, folder, PEHeaderKind.Optional, PEHeaderEntries.Optional(pe));
            var directories = PEHeaderEntries.DataDirectories(pe);
            result.Add(CreatePart(parent, MetadataTables.CreateGridPart(folder.Module, file, directories.Key, new MetadataPartKey.DataDirectories(), MetadataGridRole.Directory,
                directories)));
            result.Add(CreatePart(parent, MetadataTables.CreateDebugDirectoryPart(folder.Module, pe)));
        }
        result.Add(BrowseNodes.Create(parent, new NodeSubject.MetadataTables { Folder = folder }));
        foreach (var (title, heap) in MetadataHeaps.Create(file))
        {
            result.Add(CreatePart(parent, MetadataTables.CreateGridPart(folder.Module, file, title, new MetadataPartKey.Heap(heap.Key), MetadataGridRole.Heap, heap)));
        }

        return result.ToArray();
    }

    private static void AddHeader(BrowseNode parent, List<BrowseNode> result, MetadataFolderInfo folder, PEHeaderKind which, GridSchema header)
    {
        result.Add(CreatePart(parent, MetadataTables.CreateGridPart(folder.Module, folder.File, header.Key, new MetadataPartKey.Header(which), MetadataGridRole.Header, header)));
    }

    /// <summary>The entries of the debug directory: what ILSpy decodes of each, else its raw bytes; the
    /// embedded PDB as the metadata folder of the debug metadata the module already loaded.</summary>
    internal static BrowseNode[] ListDebugEntries(BrowseNode parent, LoadedModule module, PEFile file)
    {
        var reader = file.Reader;
        var result = new List<BrowseNode>();
        var entries = reader.ReadDebugDirectory();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            object? decoded;
            var failed = false;
            try
            {
                decoded = ReadDecoded(reader, module, entry);
            }
            catch (BadImageFormatException)
            {
                // An entry that cannot be decoded shows its raw bytes, as one of a type ILSpy doesn't decode.
                decoded = null;
                failed = true;
            }

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
