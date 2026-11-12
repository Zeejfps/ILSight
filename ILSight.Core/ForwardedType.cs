using System.Reflection.Metadata;

namespace ILSight.Core;

/// <summary>A top-level type an assembly forwards to another: its exported type row, its namespace,
/// its name as the tree shows it (<c>Dictionary&lt;,&gt;</c>), its name without type parameters and how
/// many it has, which it sorts by, and the assembly it is forwarded to.</summary>
internal sealed record ForwardedType
{
    public required ExportedTypeHandle Handle { get; init; }

    public required string Namespace { get; init; }

    public required string Label { get; init; }

    public required string SortName { get; init; }

    public required int Arity { get; init; }

    public required string Destination { get; init; }
}
