using ILSight.NuGetBrowsing;

namespace ILSight;

/// <summary>A search <see cref="NuGetBrowser.StartSearch"/> started, for <see cref="NuGetBrowser.SearchAsync"/> to run:
/// what it asks, and the work it runs as, which a newer search or closing the dialog cancels.</summary>
[MustUse]
public sealed class FeedSearchTicket
{
    public required FeedSearch Search { get; init; }

    public required WorkTicket Work { get; init; }
}
