
namespace ILSight.AssemblyListArea;

/// <summary>An assembly list as a settings file can hold it. Without a name it is no list.</summary>
internal sealed record AssemblyListJson
{
    public string? Name { get; init; }

    public List<string?>? Assemblies { get; init; }

    public List<CodeAnchorJson?>? Analyzed { get; init; }

    public Dictionary<string, string?>? Pdbs { get; init; }

    public Dictionary<string, string?>? Frameworks { get; init; }

    public DocumentsJson? Documents { get; init; }
}
