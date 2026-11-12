using System.Text.Json;

namespace ILSight.BookmarkArea;

/// <summary>The real ILSpy's bookmarks file as it can hold them, each bookmark read on its own.</summary>
internal sealed record BookmarkFileJson
{
    public int Version { get; init; }

    public List<JsonElement>? Bookmarks { get; init; }
}
