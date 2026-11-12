using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>A bookmark as this app's settings or the real ILSpy's bookmarks file can hold it.</summary>
internal sealed record BookmarkJson
{
    public string? Name { get; init; }

    public bool Enabled { get; init; }

    public string? FileName { get; init; }

    public string? AssemblyFullName { get; init; }

    public string? ModuleName { get; init; }

    public uint Token { get; init; }

    public BookmarkKind Kind { get; init; }

    public int ILOffset { get; init; }

    public int LineNumber { get; init; }

    public string? MemberName { get; init; }

    public string? LocationNodeName { get; init; }

    public BookmarkViewStateJson? ViewState { get; init; }

    public DecompilationLanguage? Language { get; init; }

    public SavedPathJson? Path { get; init; }
}
