using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>
/// A bookmark as the real ILSpy keeps it in ILSpy.Bookmarks.json, so files go both ways.
/// <see cref="LineNumber"/> counts from 1. <see cref="Location"/> and <see cref="Language"/> are this
/// app's own — where in its tree the node the bookmark was made in is, and that document's language —
/// which ILSpy skips; a record without a location finds one through
/// <see cref="BookmarkViewState.SelectedTreeNodePath"/>.
/// </summary>
public sealed record BookmarkRecord
{
    public required string Name { get; init; }

    public required bool Enabled { get; init; }

    public required AssemblyPath FileName { get; init; }

    public required string AssemblyFullName { get; init; }

    public required string ModuleName { get; init; }

    public required uint Token { get; init; }

    public required BookmarkKind Kind { get; init; }

    public required int ILOffset { get; init; }

    public required int LineNumber { get; init; }

    public required string MemberName { get; init; }

    public required string? LocationNodeName { get; init; }

    public required BookmarkViewState? ViewState { get; init; }

    public NodePath? Location { get; init; }

    public DecompilationLanguage? Language { get; init; }
}
