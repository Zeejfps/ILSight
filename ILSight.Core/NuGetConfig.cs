namespace ILSight.Core;

/// <summary>
/// What the user's NuGet.Config says about where packages come from: the enabled sources that speak
/// the v3 protocol over HTTP, and the global packages folder that restores fill. A config that is
/// missing is as NuGet's default (<see cref="NuGetConfigs.CreateDefault"/>); one that can't be read is
/// <see cref="NuGetConfigRead.Unreadable"/>.
/// </summary>
public sealed record NuGetConfig
{
    public required IReadOnlyList<NuGetSource> Sources { get; init; }

    public required string GlobalPackagesFolder { get; init; }
}
