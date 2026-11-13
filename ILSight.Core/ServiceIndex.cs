namespace ILSight.Core;

/// <summary>The resources of the feed's service index this reads; a feed may describe no versions.</summary>
public sealed record ServiceIndex
{
    public required WebUrl Search { get; init; }

    public required WebUrl PackageFolder { get; init; }

    public required WebUrl? Registrations { get; init; }

    /// <summary>Where each resource the index lists that couldn't be read was (<c>resources[2]</c>).</summary>
    public required IReadOnlyList<string> LeftOut { get; init; }
}
