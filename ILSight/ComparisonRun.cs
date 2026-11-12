using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight;

/// <summary>A comparison <see cref="Comparisons"/> started, for <see cref="CompareAssembliesHandler"/> to run: the
/// comparison, its two assemblies as they were open when it started, what it takes in, and the work it runs as, which
/// a newer comparison of it or closing its tab cancels.</summary>
[MustUse]
public sealed class ComparisonRun
{
    public required AssemblyComparison Comparison { get; init; }

    public required LoadedModule Left { get; init; }

    public required LoadedModule Right { get; init; }

    public required ApiScope Scope { get; init; }

    public required WorkTicket Work { get; init; }
}
