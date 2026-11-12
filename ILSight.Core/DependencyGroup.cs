namespace ILSight.Core;

/// <summary>The packages a version depends on when used from <see cref="TargetFramework"/>, empty for any framework.</summary>
public sealed record DependencyGroup
{
    public required string TargetFramework { get; init; }

    public required IReadOnlyList<PackageDependency> Dependencies { get; init; }
}
