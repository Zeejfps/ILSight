namespace ILSight.Core;

/// <summary>A folder of a listed package, whose files can be extracted: the package and the folder.</summary>
public sealed record PackageFolderPlace(LoadedPackage Container, PackageFolder Folder);
