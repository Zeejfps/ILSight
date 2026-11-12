using ILSight.Core.Comparison;

namespace ILSight.SettingsFormat;

internal sealed record ComparisonDocumentJson(
    string? Panel,
    string? Title,
    string? LeftPath,
    string? RightPath,
    ApiScope Scope,
    ComparisonViewJson? View) : DocumentJson(Panel, Title);
