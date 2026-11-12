namespace ILSight.Core;

/// <summary>Why a version is deprecated (such as Legacy or CriticalBugs), what its owners say, and the
/// package to use instead, empty when they name none.</summary>
public sealed record PackageDeprecation
{
    public required IReadOnlyList<string> Reasons { get; init; }

    public required string Message { get; init; }

    public required string AlternatePackage { get; init; }
}
