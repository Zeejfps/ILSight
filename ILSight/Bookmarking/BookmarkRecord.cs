using ILSight.Core;

namespace ILSight.Bookmarking;

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
