using ILSight.Core;

namespace ILSight.Bookmarking;

/// <summary>A bookmark as this app's settings or the real ILSpy's bookmarks file can hold it.</summary>
internal sealed record BookmarkJson(
    string? Name,
    bool Enabled,
    string? FileName,
    string? AssemblyFullName,
    string? ModuleName,
    uint Token,
    BookmarkKind Kind,
    int ILOffset,
    int LineNumber,
    string? MemberName,
    string? LocationNodeName,
    BookmarkViewStateJson? ViewState,
    DecompilationLanguage? Language,
    SavedPathJson? Path);
