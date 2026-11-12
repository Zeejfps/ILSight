namespace ILSight.Core;

/// <summary>A ready-made assembly list offered to add: the assemblies of a framework installed here.</summary>
public sealed record AssemblyListPreset
{
    public required string Name { get; init; }

    public required IReadOnlyList<AssemblyPath> Paths { get; init; }
}
