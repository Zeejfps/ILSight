namespace ILSight.Core;

/// <summary>Where an assembly read out of a package came from: the package, as listed, and its file there.</summary>
public sealed record PackagePlace(LoadedPackage Container, PackageFile File);
