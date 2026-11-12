namespace ILSight.CodeView;

/// <summary>Where a bracket and the one matching it are in a document.</summary>
public readonly record struct BracketPair(TextPosition Open, TextPosition Close);
