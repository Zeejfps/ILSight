using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight;

/// <summary>A tab comparing the API of the assembly at <see cref="LeftPath"/> with that of the one at
/// <see cref="RightPath"/>, taking in what <see cref="Scope"/> says, and how it was viewed.</summary>
public sealed record ComparisonDocumentRecord(
    string Panel,
    string? Title,
    AssemblyPath LeftPath,
    AssemblyPath RightPath,
    ApiScope Scope,
    ComparisonViewRecord? View) : DocumentRecord(Panel, Title);
