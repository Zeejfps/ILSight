namespace ILSight.Core;

/// <summary>The packages a version depends on when used from <see cref="TargetFramework"/>, empty for any framework.</summary>
public sealed record DependencyGroup(string TargetFramework, IReadOnlyList<PackageDependency> Dependencies);
