namespace ILSight;

/// <summary>A request <see cref="Navigation.BeginResolving"/> began, whose place a lookup finds: the intent it took,
/// which a newer request makes stale, and where what the lookup finds shows. While its lookup runs the app says it is
/// resolving; dropped, it says so until the next request.</summary>
[MustUse]
public sealed class ResolveTicket
{
    public required int Intent { get; init; }

    public required TabDestination Destination { get; init; }
}
