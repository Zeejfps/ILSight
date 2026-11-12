using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace ILSight.Tests;

/// <summary>Assemblies that load but hold something that cannot be read.</summary>
internal static class BrokenAssemblies
{
    public const string AvaloniaContainer = "!AvaloniaResources";

    /// <summary>An assembly whose Avalonia asset container is of a version no reader knows, so the
    /// container's children cannot be built.</summary>
    public static string WriteUnreadableAvaloniaContainer(TempDir temp)
    {
        byte[] container = [8, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 0];
        var metadata = new MetadataBuilder();
        var name = metadata.GetOrAddString("Unreadable");
        metadata.AddModule(0, name, metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(name, new Version(1, 0), default, default, 0, AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(default, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddManifestResource(ManifestResourceAttributes.Public, metadata.GetOrAddString(AvaloniaContainer), default, 0);

        var resources = new BlobBuilder();
        resources.WriteInt32(container.Length);
        resources.WriteBytes(container);
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder(),
            managedResources: resources);
        var image = new BlobBuilder();
        pe.Serialize(image);
        return temp.Write("Unreadable.dll", image.ToArray());
    }

    /// <summary>A copy of the assembly at <paramref name="path"/> whose first parameter's name points past its string
    /// heap: the file opens and lists its types, and reading that one cell of its metadata throws.</summary>
    public static string WriteUnreadableParameterName(TempDir temp, string path)
    {
        var image = File.ReadAllBytes(path);
        using (var pe = new PEReader(new MemoryStream(image)))
        {
            var reader = pe.GetMetadataReader();
            var stringHeapSize = reader.GetHeapSize(HeapIndex.String);
            var stringIndexSize = stringHeapSize < 0x10000 ? 2 : 4;
            const int flagsAndSequenceSize = 4;
            var offset = pe.PEHeaders.MetadataStartOffset + reader.GetTableMetadataOffset(TableIndex.Param) + flagsAndSequenceSize;
            image.AsSpan(offset, stringIndexSize).Fill(0xFF);
        }

        return temp.Write("UnreadableParameter.dll", image);
    }

    /// <summary>A copy of the assembly at <paramref name="path"/> whose first type after <c>&lt;Module&gt;</c> has a
    /// namespace that points past its string heap: the file opens, and reading its type system, which lists the
    /// namespaces, throws.</summary>
    public static string WriteUnreadableTypeSystem(TempDir temp, string path)
    {
        var image = File.ReadAllBytes(path);
        using (var pe = new PEReader(new MemoryStream(image)))
        {
            var reader = pe.GetMetadataReader();
            var rowSize = reader.GetTableRowSize(TableIndex.TypeDef);
            var stringHeapSize = reader.GetHeapSize(HeapIndex.String);
            var stringIndexSize = stringHeapSize < 0x10000 ? 2 : 4;
            const int flagsSize = 4;
            const int secondRow = 1;
            var offset = pe.PEHeaders.MetadataStartOffset + reader.GetTableMetadataOffset(TableIndex.TypeDef) + rowSize * secondRow + flagsSize +
                         stringIndexSize;
            image.AsSpan(offset, stringIndexSize).Fill(0xFF);
        }

        return temp.Write("UnreadableTypes.dll", image);
    }
}
