namespace ILSight.Core;

/// <summary>One item of a list: the term it names, when it names one, and its description.</summary>
public sealed record DocListItem
{
    public required IReadOnlyList<DocRun> Term { get; init; }

    public required IReadOnlyList<DocRun> Description { get; init; }
}
