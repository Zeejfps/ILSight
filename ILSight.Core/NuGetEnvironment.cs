namespace ILSight.Core;

/// <summary>What a NuGet.Config means depends on beyond the file itself: the user's home folder, the
/// <c>NUGET_PACKAGES</c> variable, and the environment variables a path in the config may name.</summary>
public sealed record NuGetEnvironment
{
    /// <summary>The user's home folder, which the default global packages folder is in.</summary>
    public required string HomeFolder { get; init; }

    /// <summary>The value of <c>NUGET_PACKAGES</c>, or null where it is not set.</summary>
    public required string? PackagesFolderVariable { get; init; }

    /// <summary>A path with the environment variables it names, as <c>%NAME%</c>, replaced by their values.</summary>
    public required Func<string, string> ExpandVariables { get; init; }
}
