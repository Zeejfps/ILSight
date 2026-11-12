namespace ILSight.Core;

/// <summary>A package depended on, and the versions of it that do, as NuGet writes a range.</summary>
public sealed record PackageDependency(string Id, string Range);
