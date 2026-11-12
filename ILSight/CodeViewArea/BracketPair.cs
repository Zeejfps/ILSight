namespace ILSight.CodeViewArea;

/// <summary>Where a bracket and the one matching it are in a document.</summary>
public readonly record struct BracketPair
{
    public required TextPosition Open { get; init; }

    public required TextPosition Close { get; init; }
}
