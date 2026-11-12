using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>What an <see cref="AssemblyList"/> holds at a moment, replaced whole, so that no reader sees one part of a
/// change without the rest. <see cref="Arriving"/> holds the files asked for that haven't landed (a path once per
/// asking), which leave it as they are listed, stop arriving or the list is cleared.</summary>
internal sealed record AssemblyListSnapshot
{
    public required ImmutableArray<LoadedAssembly> Listed { get; init; }

    public required ImmutableArray<LoadedModule> Loaded { get; init; }

    public required int Version { get; init; }

    public required int Generation { get; init; }

    public required AssemblyLoadOptions ReadOptions { get; init; }

    public required ImmutableArray<AssemblyPath> Arriving { get; init; }

    public static AssemblyListSnapshot Empty { get; } = new AssemblyListSnapshot
    {
        Listed = ImmutableArray<LoadedAssembly>.Empty,
        Loaded = ImmutableArray<LoadedModule>.Empty,
        Version = 0,
        Generation = 0,
        ReadOptions = AssemblyLoadOptions.Default,
        Arriving = ImmutableArray<AssemblyPath>.Empty,
    };
}
