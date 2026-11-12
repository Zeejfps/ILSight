namespace ILSight.Core;

/// <summary>The resources of the feed's service index this reads; a feed may describe no versions.</summary>
public sealed record ServiceIndex(WebUrl Search, WebUrl PackageFolder, WebUrl? Registrations);
