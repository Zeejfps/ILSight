namespace ILSight.Core;

/// <summary>
/// Where the packages taken from a feed are kept. One a restore already put in the global packages
/// folder is opened from there; any other is downloaded into a folder of the app's own, laid out the
/// same way, and kept there, so that a list holding it opens again with no feed to reach.
/// </summary>
public sealed class PackageCache
{
    /// <summary>The global packages folder a restore puts packages in.</summary>
    public required string GlobalPackagesFolder { get; init; }

    /// <summary>The folder of the app's own that packages are downloaded into.</summary>
    public required string DownloadFolder { get; init; }

    /// <summary>The folder the app downloads packages into for the user running it.</summary>
    public static string UserDownloadFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ILSight", "packages");
}
