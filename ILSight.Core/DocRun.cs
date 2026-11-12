namespace ILSight.Core;

public readonly record struct DocRun
{
    public required string Text { get; init; }

    public required DocStyle Style { get; init; }

    public CodeReference? Reference { get; init; }
}
