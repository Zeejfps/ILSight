namespace ILSight.BookmarkArea;

/// <summary>A bookmark an import left out, as ILSpy saved it: its name, where it pointed and its file, the values put
/// into the language's template as they are (<see cref="Words"/>).</summary>
public sealed record LostBookmark
{
    public required string Name { get; init; }

    public required string Location { get; init; }

    public required string File { get; init; }

    /// <summary>The bookmark in <paramref name="t"/>'s language: its template's punctuation and order, the values as
    /// they are.</summary>
    public string Words(Strings t)
    {
        return t.BookmarkLost(name: Name, location: Location, file: File);
    }

    /// <summary><paramref name="lost"/> in <paramref name="t"/>'s language, joined by its list separator.</summary>
    public static string JoinWords(Strings t, IEnumerable<LostBookmark> lost)
    {
        return string.Join(t.ListSeparator, lost.Select(b => b.Words(t)));
    }
}
