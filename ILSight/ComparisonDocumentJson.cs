using ILSight.Core.Comparison;

namespace ILSight;

internal sealed record ComparisonDocumentJson : DocumentJson
{
    /// <summary>The name of the assembly its title is worded around; null in settings saved before, which held the
    /// title worded (<see cref="DocumentJson.Title"/>).</summary>
    public string? TitleName { get; init; }

    public string? LeftPath { get; init; }

    public string? RightPath { get; init; }

    public ApiScope Scope { get; init; }

    public ComparisonViewJson? View { get; init; }
}
