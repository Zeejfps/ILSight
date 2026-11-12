namespace ILSight.Core;

/// <summary>A run of a <see cref="DocumentLine"/>'s text: characters <see cref="Start"/> up to
/// <see cref="End"/>, optionally pointing at a <see cref="Reference"/>, which this run defines when
/// <see cref="IsDefinition"/> is set.</summary>
public readonly record struct DocumentSpan
{
    public required int Start { get; init; }

    public required int Length { get; init; }

    public required SpanKind Kind { get; init; }

    public required CodeReference? Reference { get; init; }

    public required bool IsDefinition { get; init; }

    public int End => Start + Length;
}
