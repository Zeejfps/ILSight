namespace ILSight.Bookmarking;

/// <summary>Bookmarks read from a file, waiting for the user to say whether they replace the ones
/// there are or merge in beside them.</summary>
public sealed record PendingImport(IReadOnlyList<Bookmark> Bookmarks);
