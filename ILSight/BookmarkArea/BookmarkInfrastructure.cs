using ILSight.Core;

namespace ILSight.BookmarkArea;

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

    /// <summary>Looks on the thread pool and comes back on the UI thread, the first place the library failed to find
    /// as the answer's <see cref="Outcome{T}.Failed"/>, as the core answers it; the cancellation of <paramref name="ct"/>
    /// is let through. A record with this app's place is where it says; one of the real ILSpy's is where ILSpy's tree
    /// path, token or member name lead.</summary>
    public async Task<Outcome<List<Lookup<NodePath>>>> LookUpPlacesAsync(IReadOnlyList<BookmarkRecord> records, CancellationToken ct)
    {
        var found = await Background.Wait(Task.Run(() => FindPlacesAsync(records, ct), ct));
        ct.ThrowIfCancellationRequested();
        return found;
    }

    private async Task<Outcome<List<Lookup<NodePath>>>> FindPlacesAsync(IReadOnlyList<BookmarkRecord> records, CancellationToken ct)
    {
        var places = new List<Lookup<NodePath>>(records.Count);
        foreach (var record in records)
        {
            if (record.Location is not null)
            {
                places.Add(new Lookup<NodePath>.Found { Value = record.Location });
                continue;
            }

            var treePath = record.ViewState?.SelectedTreeNodePath;
            var node = await ILSpyTree.LocateAsync(assemblies, session, record.FileName, treePath, record.Token, record.MemberName, ct);
            if (node is Outcome<Lookup<BrowseNode>>.Failed failed)
            {
                return new Outcome<List<Lookup<NodePath>>>.Failed(failed.Error);
            }

            places.Add(((Outcome<Lookup<BrowseNode>>.Ok)node).Value.Select(static n => n.Path));
        }
        return new Outcome<List<Lookup<NodePath>>>.Ok(places);
    }
}
