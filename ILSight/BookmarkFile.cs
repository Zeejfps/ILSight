using System.Text.Json;
using ILSight.SettingsFormat;

namespace ILSight;

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
