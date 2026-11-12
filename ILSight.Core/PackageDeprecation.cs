namespace ILSight.Core;

/// <summary>Why a version is deprecated (such as Legacy or CriticalBugs), what its owners say, and the
/// package to use instead, empty when they name none.</summary>
public sealed record PackageDeprecation(IReadOnlyList<string> Reasons, string Message, string AlternatePackage);
