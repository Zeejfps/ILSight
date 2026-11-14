using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>Goes to the line a bookmark marks: finds its place in the open assemblies through <see cref="IPorts"/>,
/// shows it in the active tab and records it in the history, or says the bookmark gone on the refusal line.</summary>
public sealed class GoToBookmarkHandler(GoToBookmarkHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user looking for the place a bookmark marks failed, or that it is in a module that could
        /// not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Says on the refusal line that the place a bookmark marks is gone.</summary>
        void ShowRefusal(Refusal refusal);

        /// <summary>The node at <paramref name="place"/>; not found when it is gone, unreadable when in a module whose
        /// type system the library could not read; looked for without holding up the
        /// screen (it may read an assembly from disk); a lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Lookup<BrowseNode>>> LookUpPlaceAsync(NodePath place, CancellationToken ct);
    }

    /// <summary>Shows the line <paramref name="bookmark"/> marks in the active tab, opening its assembly first if it
    /// is not open, and records it in the history; says the bookmark gone when its place is. <paramref name="ticket"/>
    /// is the request the edge began (<see cref="Navigation.BeginResolving"/>), which it ends, as failed, should this
    /// throw. Returns the load that started in the tab, for the edge that started this to start under the tab's work;
    /// null when none started, as when something else was asked for meanwhile.</summary>
    public async Task<DecompileTicket?> HandleAsync(Navigation navigation, BookmarkList bookmarks, DecompilerChoice decompiler, Bookmark bookmark,
        ResolveTicket ticket, CancellationToken ct)
    {
        var found = await ports.LookUpPlaceAsync(bookmark.Place, ct);
        var resolved = navigation.EndResolving(ticket.Intent, found);
        return ShowBookmarkPlace(navigation, bookmarks, decompiler, bookmark, resolved, ticket.Destination);
    }

    private DecompileTicket? ShowBookmarkPlace(Navigation navigation, BookmarkList bookmarks, DecompilerChoice decompiler, Bookmark bookmark,
        Resolution<BrowseNode> resolved, TabDestination destination)
    {
        return resolved.Match<DecompileTicket?>(
            found => ShowBookmarkedNode(navigation, bookmarks, decompiler, bookmark, found.Value, destination),
            _ =>
            {
                ports.ShowRefusal(new Refusal.BookmarkGone { Bookmark = bookmark.Name, Place = bookmark.Place });
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

    private static DecompileTicket? ShowBookmarkedNode(Navigation navigation, BookmarkList bookmarks, DecompilerChoice decompiler, Bookmark bookmark,
        BrowseNode node, TabDestination destination)
    {
        bookmarks.Resolve(bookmark, node);
        return navigation.ShowFoundNode(destination, node, decompiler.Setup, bookmark);
    }

    private void ReportBookmarkGone(Bookmark bookmark, Exception error)
    {
        ports.ReportError(new AppProblem.BookmarkGone
        {
            Bookmark = bookmark.Name,
            Place = bookmark.Place,
            Error = error,
        });
    }
}
