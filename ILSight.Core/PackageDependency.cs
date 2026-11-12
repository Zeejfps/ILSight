namespace ILSight.Core;

/// <summary>A package depended on, and the versions of it that do, as NuGet writes a range.</summary>
public sealed record PackageDependency
{
    public required string Id { get; init; }

    public required string Range { get; init; }
}
