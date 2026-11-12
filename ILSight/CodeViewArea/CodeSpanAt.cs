namespace ILSight.CodeViewArea;

/// <summary>The span <see cref="Span"/> of the line <see cref="Line"/> of a document.</summary>
internal readonly record struct CodeSpanAt
{
    public required int Line { get; init; }

    public required int Span { get; init; }
}
