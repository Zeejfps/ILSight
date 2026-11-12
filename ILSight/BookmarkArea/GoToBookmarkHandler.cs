using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>Goes to the line a bookmark marks: finds its place in the open assemblies through <see cref="IPorts"/>,
/// shows it in the active tab and records it in the history, or reports the bookmark gone.</summary>
public sealed class GoToBookmarkHandler(GoToBookmarkHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the place a bookmark marks is gone, or in a module that could not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The node at <paramref name="place"/>; not found when it is gone, unreadable when in a module whose
        /// type system the library could not read; looked for without holding up the
        /// screen (it may read an assembly from disk); a lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Lookup<BrowseNode>>> LookUpPlaceAsync(NodePath place, CancellationToken ct);
    }

    /// <summary>Shows the line <paramref name="bookmark"/> marks in the active tab, opening its assembly first if it
    /// is not open, and records it in the history; reports the bookmark gone when its place is. Returns the load that
    /// started in the tab, for the edge that started this to start under the tab's work; null when none started,
    /// as when something else was asked for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(AppState appState, Bookmark bookmark, CancellationToken ct)
    {
        var ticket = appState.Navigation.BeginResolving(inNewTab: false);
        var found = await ports.LookUpPlaceAsync(bookmark.Place, ct);
        var resolved = appState.Navigation.EndResolving(ticket.Intent, found);
        return ShowBookmarkPlace(appState, bookmark, resolved, ticket.Destination);
    }

    /// <summary>Shows the place <paramref name="resolved"/> found, or reports the bookmark gone. Returns the load it
    /// started in the tab; null when it started none.</summary>
    private DecompileTicket? ShowBookmarkPlace(AppState appState, Bookmark bookmark, Resolution<BrowseNode> resolved, TabDestination destination)
    {
        return resolved.Match<DecompileTicket?>(
            found => ShowBookmarkedNode(appState, bookmark, found.Value, destination),
            _ =>
            {
                ReportBookmarkGone(bookmark, null);
                return null;
            },
            unreadable =>
            {
                ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
                return null;
            },
            failed =>
            {
                ReportBookmarkGone(bookmark, failed.Error);
                return null;
            },
            static _ => null);
    }

    private static DecompileTicket? ShowBookmarkedNode(AppState appState, Bookmark bookmark, BrowseNode node, TabDestination destination)
    {
        appState.Bookmarks.Resolve(bookmark, node);
        return appState.Navigation.ShowFoundNode(destination, node, appState.Decompiler.Setup, bookmark);
    }

    private void ReportBookmarkGone(Bookmark bookmark, Exception? error)
    {
        ports.ReportError(new AppProblem.BookmarkGone
        {
            Bookmark = bookmark.Name,
            Place = bookmark.Place,
            Error = error,
        });
    }
}
