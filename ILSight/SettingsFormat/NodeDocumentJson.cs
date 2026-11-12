using ILSight.Core;
using ILSight.CodeView;

namespace ILSight.SettingsFormat;

internal sealed record NodeDocumentJson(
    string? Panel,
    string? Title,
    string? FilePath,
    SavedPathJson? Path,
    DecompilationLanguage Language,
    CodeViewPositionJson? Position,
    GridPositionJson? GridPosition) : DocumentJson(Panel, Title);
