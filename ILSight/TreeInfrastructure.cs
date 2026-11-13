using ILSight.Core;

namespace ILSight;

/// <summary>Builds the children of the browse tree's nodes off the UI thread, for <see cref="BuildChildrenHandler"/>
/// and <see cref="ExpandNamespacesHandler"/>. A build the library failed comes back as
/// <see cref="ChildrenState.Failed"/>, as the core answers it; the cancellation of the token the call was given is let
/// through.</summary>
public sealed class TreeInfrastructure(BrowseSession session) : BuildChildrenHandler.IPorts, ExpandNamespacesHandler.IPorts
{
    /// <summary>Builds them on the thread pool and comes back on the UI thread; a build that failed keeps why in the
    /// node's children.</summary>
    public async Task BuildChildrenAsync(BrowseNode node, CancellationToken ct)
    {
        await Background.Wait(BrowseSessions.GetChildrenAsync(session, node, ct));
        ct.ThrowIfCancellationRequested();
    }

    /// <summary>Builds them on the thread pool and comes back on the UI thread.</summary>
    public async Task<ChildrenState> BuildAssemblyChildrenAsync(BrowseNode assembly, CancellationToken ct)
    {
        var built = await Background.Wait(BrowseSessions.GetChildrenAsync(session, assembly, ct));
        ct.ThrowIfCancellationRequested();
        return built;
    }
}
