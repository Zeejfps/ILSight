using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight;

/// <summary>A tab comparing the API of the assembly at <see cref="LeftPath"/> with that of the one at
/// <see cref="RightPath"/>, taking in what <see cref="Scope"/> says, and how it was viewed.</summary>
public sealed record ComparisonDocumentRecord : DocumentRecord
{
    public required AssemblyPath LeftPath { get; init; }

    public required AssemblyPath RightPath { get; init; }

    public required ApiScope Scope { get; init; }

    public required ComparisonViewRecord? View { get; init; }
}
