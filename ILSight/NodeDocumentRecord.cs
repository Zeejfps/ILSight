using ILSight.Core;

namespace ILSight;

/// <summary>A tab showing the node at <see cref="Location"/>, in <see cref="Language"/>, its view standing
/// at <see cref="View"/>.</summary>
public sealed record NodeDocumentRecord : DocumentRecord
{
    public required NodePath Location { get; init; }

    public required DecompilationLanguage Language { get; init; }

    public required ViewPosition? View { get; init; }
}
