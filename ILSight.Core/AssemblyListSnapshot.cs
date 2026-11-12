using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>What an <see cref="AssemblyList"/> holds at a moment, replaced whole, so that no reader sees one part of a
/// change without the rest.</summary>
internal sealed record AssemblyListSnapshot(ImmutableArray<LoadedAssembly> Listed, ImmutableArray<LoadedModule> Loaded, int Version, int Generation, AssemblyLoadOptions ReadOptions)
{
    public static AssemblyListSnapshot Empty { get; } = new(ImmutableArray<LoadedAssembly>.Empty, ImmutableArray<LoadedModule>.Empty, 0, 0, AssemblyLoadOptions.Default);
}
