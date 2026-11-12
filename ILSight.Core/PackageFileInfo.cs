using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>A file of a package that is no assembly: shown as text, a picture or its bytes, read out of
/// the package when needed.</summary>
public sealed class PackageFileInfo
{
    public required LoadedPackage Package { get; init; }

    public required PackageFile File { get; init; }

    /// <summary>Whether it is shown as a picture, being named as one is.</summary>
    public bool IsImage => ResourceImages.IsImageName(File.Name);

    public string Description => $"{File.FullName} ({File.Length} bytes)";

    public string FileName => File.Name;
}
