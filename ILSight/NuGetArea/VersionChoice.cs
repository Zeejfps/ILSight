using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>The versions a package lists to pick from, newest first, how each is written, and the one
/// picked: the package's latest at first, then whichever of those listed the user picks.</summary>
public sealed class VersionChoice
{
    private readonly PackageVersion[] _versions;
    private readonly string[] _texts;

    private VersionChoice(FeedPackage package, PackageVersion[] versions, string[] texts, PackageVersion picked)
    {
        Package = package;
        _versions = versions;
        _texts = texts;
        Picked = picked;
    }

    /// <summary>The package the versions are of.</summary>
    public FeedPackage Package { get; }

    /// <summary>The version to open.</summary>
    public PackageVersion Picked { get; }

    /// <summary>The versions the package lists, newest first.</summary>
    public ReadOnlySpan<PackageVersion> Versions => _versions;

    /// <summary>How <see cref="Versions"/> are written, in their order.</summary>
    public ReadOnlySpan<string> Texts => _texts;

    /// <summary>The versions <paramref name="package"/> lists, its latest picked.</summary>
    public static VersionChoice FromPackage(FeedPackage package)
    {
        var versions = package.Versions
            .Select(static v => v.Version)
            .ToArray();
        var texts = versions
            .Select(static v => v.Text)
            .ToArray();
        return new VersionChoice(package, versions, texts, package.Version);
    }

    /// <summary>The same versions with <paramref name="version"/>, which must be one of them, picked.</summary>
    public VersionChoice WithPicked(PackageVersion version)
    {
        if (!_versions.Contains(version))
        {
            throw new ArgumentException($"Version '{version}' is not one {Package.Id} lists.", nameof(version));
        }

        return new VersionChoice(Package, _versions, _texts, version);
    }
}
