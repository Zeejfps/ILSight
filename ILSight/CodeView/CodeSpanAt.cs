namespace ILSight.CodeView;

/// <summary>The span <see cref="Span"/> of the line <see cref="Line"/> of a document.</summary>
internal readonly record struct CodeSpanAt(int Line, int Span);
