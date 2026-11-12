namespace ILSight.Core;

/// <summary>A declaration as one line of code, where in it each parameter starts, so it can be
/// shown with a parameter per line when the line is too long, and its type parameters' constraints,
/// a <c>where</c> clause a line.</summary>
public sealed record CodeSignature
{
    public required DocumentLine Line { get; init; }

    public required IReadOnlyList<int> ParameterStarts { get; init; }

    public required IReadOnlyList<DocumentLine> Constraints { get; init; }
}
