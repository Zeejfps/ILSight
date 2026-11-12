using ILSight.Core;

namespace ILSight;

/// <summary>Reaches the app's error reports, the bookmarks files and the open assemblies off the UI thread, for
/// <see cref="BookmarkImporting"/> and <see cref="ExportBookmarksHandler"/>.</summary>
public sealed class BookmarkInfrastructure(ErrorLog errors, AssemblyList assemblies, BrowseSession session)
    : BookmarkImporting.IPorts, ExportBookmarksHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public BookmarksRead ReadBookmarks(string path)
    {
        return BookmarkFile.Read(path);
    }

    public BookmarksWriteResult WriteBookmarks(string path, List<BookmarkRecord> records)
    {
        return BookmarkFile.Write(path, records);
    }

    /// <summary>Looks on the thread pool and comes back on the UI thread, with whatever the lookup threw as
    /// <see cref="Outcome{T}.Failed"/>, except the cancellation of <paramref name="ct"/>, which it lets through. A
    /// record with this app's place is where it says; one of the real ILSpy's is where ILSpy's tree path, token or
    /// member name lead.</summary>
    public async Task<Outcome<List<NodePath?>>> LookUpPlacesAsync(IReadOnlyList<BookmarkRecord> records, CancellationToken ct)
    {
        var found = await Background.Wait(Task.Run(() => LookUpPlaces(records), ct));
        ct.ThrowIfCancellationRequested();
        return found;
    }

    private List<NodePath?> LookUpPlaces(IReadOnlyList<BookmarkRecord> records)
    {
        return records
            .Select(FindPlace)
            .ToList();
    }

    private NodePath? FindPlace(BookmarkRecord record)
    {
        if (record.Location is not null)
        {
            return record.Location;
        }

        var treePath = record.ViewState?.SelectedTreeNodePath;
        var node = ILSpyTree.Locate(assemblies, session, record.FileName, treePath, record.Token, record.MemberName);
        if (node is null)
        {
            return null;
        }

        return BrowseNodes.GetPath(node);
    }
}
