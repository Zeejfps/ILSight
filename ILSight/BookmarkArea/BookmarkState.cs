using ILSight.Core;

namespace ILSight.BookmarkArea;

/// <summary>What of a <see cref="Bookmark"/> changes after it is made: its name, its place and whether it is
/// enabled. Only the <see cref="BookmarkList"/> that made the bookmark holds it, and changes it under the list's rule
/// (each change counted, a name never blank); the bookmark handed out only reads it.</summary>
public sealed class BookmarkState
{
    /// <summary>Where the document the bookmark was made in is: as it was read until the bookmark is gone to, then
    /// where its node was found.</summary>
    public required NodePath Place { get; set; }

    public required string Name { get; set; }

    /// <summary>A disabled bookmark stays listed and marked, dimmed, but next and previous skip it.</summary>
    public required bool Enabled { get; set; }
}
