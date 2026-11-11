using System.Diagnostics;
using System.Text.Json;
using ILSight.Core;

namespace ILSight;

/// <summary>How the real ILSpy anchors a bookmark: to a definition's metadata token, to an IL offset
/// in a method body, or to a line number of the document of a node.</summary>
public enum BookmarkKind
{
    Token,
    Body,
    Line,
}

/// <summary>A folded region left open in ILSpy's view when it made the bookmark.</summary>
public sealed record BookmarkFoldingRange(int Start, int End);

/// <summary>ILSpy's display settings that shaped the text the bookmark was made in.</summary>
public sealed record BookmarkRenderedLayoutSettings(
    bool FoldBraces,
    bool ExpandXmlDocumentationComments,
    bool ExpandMemberDefinitions,
    bool ExpandUsingDeclarations,
    bool ShowDebugInfo,
    bool IndentationUseTabs,
    int IndentationSize,
    int IndentationTabSize);

/// <summary>ILSpy's view of the document when the bookmark was made; <see cref="SelectedTreeNodePath"/>
/// is the path ILSpy's tree finds the bookmark's node by, which ILSpy needs to go to it.</summary>
public sealed record BookmarkViewState(
    int Version,
    int CaretOffset,
    double VerticalOffset,
    double HorizontalOffset,
    int? FoldingChecksum,
    List<BookmarkFoldingRange>? ExpandedFoldings,
    BookmarkRenderedLayoutSettings? RenderedLayoutSettings = null,
    List<string>? SelectedTreeNodePath = null);

/// <summary>
/// A bookmark as the real ILSpy keeps it in ILSpy.Bookmarks.json, so files go both ways.
/// <see cref="LineNumber"/> counts from 1. <see cref="Location"/> and <see cref="Language"/> are this
/// app's own — where in its tree the node the bookmark was made in is, and that document's language —
/// which ILSpy skips; a record without a location finds one through
/// <see cref="BookmarkViewState.SelectedTreeNodePath"/>.
/// </summary>
public sealed record BookmarkRecord(
    string Name,
    bool Enabled,
    AssemblyPath FileName,
    string AssemblyFullName,
    string ModuleName,
    uint Token,
    BookmarkKind Kind,
    int ILOffset,
    int LineNumber,
    string MemberName,
    string? LocationNodeName,
    BookmarkViewState? ViewState,
    NodePath? Location = null,
    DecompilationLanguage? Language = null);

/// <summary>The bookmarks file the real ILSpy reads and writes.</summary>
public static class BookmarkFile
{
    public const int CurrentVersion = 1;

    /// <summary>The name offered for a new export.</summary>
    public const string SuggestedName = "ILSight.Bookmarks.json";

    /// <summary>Writes <paramref name="bookmarks"/> to <paramref name="path"/>, replacing the file
    /// whole.</summary>
    public static BookmarksWriteResult Write(string path, List<BookmarkRecord> bookmarks)
    {
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(FileOf(bookmarks), AppSettingsJson.Default.BookmarkFileJson));
            File.Move(temporary, path, overwrite: true);
            return new BookmarksWriteResult.Written();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new BookmarksWriteResult.Failed { Reason = $"{path}{Environment.NewLine}{e.Message}" };
        }
    }

    private static BookmarkFileJson FileOf(List<BookmarkRecord> bookmarks)
    {
        var json = AppSettingsJson.Default.BookmarkJson;
        var written = bookmarks.ConvertAll(b => JsonSerializer.SerializeToElement(SettingsJson.Write(b), json));
        return new BookmarkFileJson(CurrentVersion, written);
    }

    /// <summary>The bookmarks in the file at <paramref name="path"/> that name an assembly file, as
    /// ILSpy keeps them, each read on its own, so one that is not a bookmark at all is left out alone,
    /// with what was wrong with it.</summary>
    public static BookmarksRead Read(string path)
    {
        try
        {
            var file = JsonSerializer.Deserialize(File.ReadAllText(path), AppSettingsJson.Default.BookmarkFileJson);
            if (file?.Bookmarks is null)
            {
                return new BookmarksRead.NotBookmarksFile();
            }

            return ReadEach(file.Bookmarks);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new BookmarksRead.Failed { Reason = $"{path}{Environment.NewLine}{e.Message}" };
        }
    }

    private static BookmarksRead.Read ReadEach(List<JsonElement> bookmarks)
    {
        var read = new List<BookmarkRecord>();
        var problems = new List<BookmarkEntryProblem>();
        for (var i = 0; i < bookmarks.Count; i++)
        {
            var entry = i + 1;
            try
            {
                var saved = bookmarks[i].Deserialize(AppSettingsJson.Default.BookmarkJson);
                if (saved is null)
                {
                    continue;
                }

                var bookmark = SettingsJson.Parse(saved, () => problems.Add(new BookmarkEntryProblem.PlaceUnreadable { Entry = entry }));
                if (bookmark is not null)
                {
                    read.Add(bookmark);
                }
            }
            catch (Exception e) when (e is JsonException or NotSupportedException)
            {
                problems.Add(new BookmarkEntryProblem.NotABookmark { Entry = entry, Detail = e.Message });
            }
        }
        return new BookmarksRead.Read { Bookmarks = read, Problems = problems };
    }
}

/// <summary>What reading a bookmarks file gave.</summary>
public abstract record BookmarksRead
{
    private BookmarksRead()
    {
    }

    /// <summary>The file was read: the bookmarks in it that name an assembly file, and what was wrong
    /// with the entries left out, or whose place in this app's tree was.</summary>
    public sealed record Read : BookmarksRead
    {
        public required List<BookmarkRecord> Bookmarks { get; init; }

        public required IReadOnlyList<BookmarkEntryProblem> Problems { get; init; }
    }

    /// <summary>The file is JSON, but holds no list of bookmarks.</summary>
    public sealed record NotBookmarksFile : BookmarksRead;

    /// <summary>The file could not be read, or is not JSON. <see cref="Reason"/> names the file and says
    /// why, as the system or the JSON reader said it.</summary>
    public sealed record Failed : BookmarksRead
    {
        public required string Reason { get; init; }
    }

    public TResult Match<TResult>(Func<Read, TResult> read, Func<NotBookmarksFile, TResult> notBookmarksFile, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Read r => read(r),
            NotBookmarksFile n => notBookmarksFile(n),
            Failed f => failed(f),
            _ => throw new UnreachableException($"BookmarksRead.Match does not handle {GetType().Name}."),
        };
    }
}

/// <summary>What was wrong with one entry of a bookmarks file, for the screen to tell in the chosen
/// language. <see cref="Entry"/> is the entry's position in the file, from 1.</summary>
public abstract record BookmarkEntryProblem
{
    private BookmarkEntryProblem()
    {
    }

    public required int Entry { get; init; }

    /// <summary>The entry is a bookmark, but the place in this app's tree it was made in cannot be read,
    /// so the bookmark is placed as ILSpy places it.</summary>
    public sealed record PlaceUnreadable : BookmarkEntryProblem;

    /// <summary>The entry is not a bookmark at all, so it was left out. <see cref="Detail"/> is as the
    /// JSON reader said it.</summary>
    public sealed record NotABookmark : BookmarkEntryProblem
    {
        public required string Detail { get; init; }
    }

    public TResult Match<TResult>(Func<PlaceUnreadable, TResult> placeUnreadable, Func<NotABookmark, TResult> notABookmark)
    {
        return this switch
        {
            PlaceUnreadable p => placeUnreadable(p),
            NotABookmark n => notABookmark(n),
            _ => throw new UnreachableException($"BookmarkEntryProblem.Match does not handle {GetType().Name}."),
        };
    }
}

/// <summary>What writing a bookmarks file did.</summary>
public abstract record BookmarksWriteResult
{
    private BookmarksWriteResult()
    {
    }

    public sealed record Written : BookmarksWriteResult;

    /// <summary><see cref="Reason"/> names the file and says why, as the system said it.</summary>
    public sealed record Failed : BookmarksWriteResult
    {
        public required string Reason { get; init; }
    }

    public TResult Match<TResult>(Func<Written, TResult> written, Func<Failed, TResult> failed)
    {
        return this switch
        {
            Written w => written(w),
            Failed f => failed(f),
            _ => throw new UnreachableException($"BookmarksWriteResult.Match does not handle {GetType().Name}."),
        };
    }
}
