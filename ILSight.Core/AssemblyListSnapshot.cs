using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>What an <see cref="AssemblyList"/> holds at a moment, replaced whole, so that no reader sees one part of a
/// change without the rest. <paramref name="Arriving"/> holds the files asked for that haven't landed (a path once per
/// asking), which leave it as they are listed, stop arriving or the list is cleared.</summary>
internal sealed record AssemblyListSnapshot(ImmutableArray<LoadedAssembly> Listed, ImmutableArray<LoadedModule> Loaded, int Version, int Generation, AssemblyLoadOptions ReadOptions,
    ImmutableArray<AssemblyPath> Arriving)
{
    public static AssemblyListSnapshot Empty { get; } = new(ImmutableArray<LoadedAssembly>.Empty, ImmutableArray<LoadedModule>.Empty, 0, 0, AssemblyLoadOptions.Default,
        ImmutableArray<AssemblyPath>.Empty);
}
