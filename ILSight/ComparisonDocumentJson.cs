using ILSight.Core.Comparison;

namespace ILSight;

internal sealed record ComparisonDocumentJson : DocumentJson
{
    public string? LeftPath { get; init; }

    public string? RightPath { get; init; }

    public ApiScope Scope { get; init; }

    public ComparisonViewJson? View { get; init; }
}
