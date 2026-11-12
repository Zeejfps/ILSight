using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ILSight.Tests;

/// <summary>For each top-level type of an assembly, the other top-level types of it that it names.</summary>
internal sealed class TypeUseGraph
{
    private readonly Dictionary<string, HashSet<string>> _uses;

    private TypeUseGraph(Dictionary<string, HashSet<string>> uses)
    {
        _uses = uses;
    }

    public IReadOnlySet<string> Uses(string type)
    {
        Assert.True(_uses.ContainsKey(type), $"{type} is not a top-level type of the assembly.");
        return _uses[type];
    }

    public static TypeUseGraph Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var scan = new TypeUseScan(pe, metadata);
        foreach (var handle in metadata.TypeDefinitions)
        {
            scan.Type(handle);
        }

        return new TypeUseGraph(scan.Uses);
    }
}
