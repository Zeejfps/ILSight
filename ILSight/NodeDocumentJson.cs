using ILSight.Core;
using ILSight.CodeViewArea;

namespace ILSight;

internal sealed record NodeDocumentJson : DocumentJson
{
    public string? FilePath { get; init; }

    public SavedPathJson? Path { get; init; }

    public DecompilationLanguage Language { get; init; }

    public CodeViewPositionJson? Position { get; init; }

    public GridPositionJson? GridPosition { get; init; }
}
