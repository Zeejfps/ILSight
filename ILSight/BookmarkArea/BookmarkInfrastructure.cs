using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>Reaches the app's error reports, its refusal line, the bookmarks files and the open assemblies off the UI thread, for
/// <see cref="BookmarkImporter"/> and <see cref="ExportBookmarksHandler"/>.</summary>
public sealed class BookmarkInfrastructure(ErrorLog errors, RefusalLine refusals, AssemblyList assemblies, BrowseSession session)
    : BookmarkImporter.IPorts, ExportBookmarksHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    public void ShowRefusal(Refusal refusal)
    {
        refusals.Show(refusal);
    }

    public BookmarksRead ReadBookmarks(string path)
    {
        return BookmarkFile.Read(path);
    }

    public BookmarksWriteResult WriteBookmarks(string path, List<BookmarkRecord> records)
    {
        return BookmarkFile.Write(path, records);
    }

    /// <summary>Looks on the thread pool and comes back on the UI thread, each record's place as its own lookup came
    /// out: a lookup the library failed as that record's <see cref="Outcome{T}.Failed"/>, as the core answers it, the
    /// others looked up all the same; the cancellation of <paramref name="ct"/> is let through. A record with this
    /// app's place is where it says; one of the real ILSpy's is where ILSpy's tree path, token or member name
    /// lead.</summary>
    public async Task<List<Outcome<Lookup<NodePath>>>> LookUpPlacesAsync(IReadOnlyList<BookmarkRecord> records, CancellationToken ct)
    {
        var found = await Background.Wait(Task.Run(() => FindPlacesAsync(records, ct), ct));
        ct.ThrowIfCancellationRequested();
        return found;
    }

    private async Task<List<Outcome<Lookup<NodePath>>>> FindPlacesAsync(IReadOnlyList<BookmarkRecord> records, CancellationToken ct)
    {
        var places = new List<Outcome<Lookup<NodePath>>>(records.Count);
        foreach (var record in records)
        {
            if (record.Location is not null)
            {
                places.Add(new Outcome<Lookup<NodePath>>.Ok(new Lookup<NodePath>.Found { Value = record.Location }));
                continue;
            }

            var treePath = record.ViewState?.SelectedTreeNodePath;
            var node = await ILSpyTree.LocateAsync(assemblies, session, record.FileName, treePath, record.Token, record.MemberName, ct);
            places.Add(PlaceOf(node));
        }
        return places;
    }

    private static Outcome<Lookup<NodePath>> PlaceOf(Outcome<Lookup<BrowseNode>> node)
    {
        return node switch
        {
            Outcome<Lookup<BrowseNode>>.Ok ok => new Outcome<Lookup<NodePath>>.Ok(ok.Value.Select(static n => n.Path)),
            Outcome<Lookup<BrowseNode>>.Failed failed => new Outcome<Lookup<NodePath>>.Failed(failed.Error),
        };
    }
}
