using ILSight.Core;

namespace ILSight;

/// <summary>A tab showing the node at <see cref="Location"/>, in <see cref="Language"/>, its view standing
/// at <see cref="View"/>.</summary>
public sealed record NodeDocumentRecord(
    string Panel,
    string? Title,
    NodePath Location,
    DecompilationLanguage Language,
    ViewPosition? View) : DocumentRecord(Panel, Title);
