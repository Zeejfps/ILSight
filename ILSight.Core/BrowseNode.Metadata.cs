using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Metadata;
using ILSight.Core.Metadata;

namespace ILSight.Core;

public static partial class BrowseNodes
{
    /// <summary>The node of the metadata of <paramref name="file"/>, titled <paramref name="title"/>, under <paramref name="parent"/>.</summary>
    internal static BrowseNode CreateMetadataFolder(LoadedModule module, BrowseNode parent, MetadataFile file, string title)
    {
        return Create(parent, new NodeSubject.Metadata { Value = new MetadataFolderInfo { Module = module, File = file, Title = title } });
    }

    private static BrowseNode CreatePart(BrowseNode parent, MetadataTableInfo part)
    {
        return Create(parent, new NodeSubject.MetadataTable { Value = part });
    }

    /// <summary>For a PE file its headers and debug directory, then the tables and the four heaps.</summary>
    private static BrowseNode[] ListMetadata(BrowseNode parent, MetadataFolderInfo folder)
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
        result.Add(Create(parent, new NodeSubject.MetadataTables { Folder = folder }));
        foreach (var (title, heap) in MetadataHeaps.Create(file))
            result.Add(CreatePart(parent, MetadataTables.CreateGridPart(folder.Module, file, title, new MetadataPartKey.Heap(heap.Key), MetadataGridRole.Heap, heap)));
        return result.ToArray();
    }

    private static void AddHeader(BrowseNode parent, List<BrowseNode> result, MetadataFolderInfo folder, PEHeaderKind which, GridSchema header)
    {
        result.Add(CreatePart(parent, MetadataTables.CreateGridPart(folder.Module, folder.File, header.Key, new MetadataPartKey.Header(which), MetadataGridRole.Header, header)));
    }

    /// <summary>A node per table: the grid of its rows. An empty table, whose grid cannot always be read,
    /// and a table ILSpy does not list show only how many rows there are.</summary>
    private static BrowseNode[] ListTables(BrowseNode parent, MetadataFolderInfo folder)
    {
        var file = folder.File;
        var result = new List<BrowseNode>();
        foreach (var index in Enum.GetValues<TableIndex>())
        {
            var rows = file.Metadata.GetTableRowCount(index);
            var schema = rows == 0 ? null : MetadataTables.Create(index, file);
            if (schema == null)
                result.Add(CreatePart(parent, MetadataTables.CreateRowCountPart(folder.Module, file, index, rows)));
            else
                result.Add(CreatePart(parent, MetadataTables.CreateGridPart(folder.Module, file, $"{(byte)index:X2} {index} ({rows})", new MetadataPartKey.Table(index),
                    MetadataGridRole.Table, schema)));
        }
        return result.ToArray();
    }

    /// <summary>The entries of the debug directory: what ILSpy decodes of each, else its raw bytes; the
    /// embedded PDB as the metadata folder of the debug metadata the module already loaded.</summary>
    private static BrowseNode[] ListDebugEntries(BrowseNode parent, LoadedModule module, PEFile file)
    {
        var reader = file.Reader;
        var result = new List<BrowseNode>();
        var entries = reader.ReadDebugDirectory();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            try
            {
                switch (entry.Type)
                {
                    case DebugDirectoryEntryType.CodeView:
                        var codeView = reader.ReadCodeViewDebugDirectoryData(entry);
                        result.Add(CreatePart(parent, MetadataTables.CreateDecodedEntryPart(module, file, index, entry,
                            [$"GUID: {codeView.Guid}", $"Age: {codeView.Age}", $"Path: {codeView.Path}"])));
                        continue;
                    case DebugDirectoryEntryType.PdbChecksum:
                        var checksum = reader.ReadPdbChecksumDebugDirectoryData(entry);
                        result.Add(CreatePart(parent, MetadataTables.CreateDecodedEntryPart(module, file, index, entry,
                            [$"AlgorithmName: {checksum.AlgorithmName}", $"Checksum: {Convert.ToHexStringLower(checksum.Checksum.AsSpan())}"])));
                        continue;
                    case DebugDirectoryEntryType.EmbeddedPortablePdb:
                        var symbols = FindEmbeddedSymbols(module);
                        if (symbols != null)
                            result.Add(CreateMetadataFolder(module, parent, symbols.File, symbols.Title));
                        continue;
                }
            }
            catch (BadImageFormatException)
            {
            }
            result.Add(CreatePart(parent, MetadataTables.CreateRawEntryPart(module, file, index, entry)));
        }
        return result.ToArray();
    }

    /// <summary>The embedded PDB the module already loaded, or null when it could not be read.</summary>
    private static DebugSymbols? FindEmbeddedSymbols(LoadedModule module)
    {
        var symbols = LoadedAssemblies.EnsureDebugSymbols(module);
        if (symbols == null)
            return null;
        if (!symbols.File.IsEmbedded)
            return null;
        return symbols;
    }
}
