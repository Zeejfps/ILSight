namespace ILSight.Core;

/// <summary>What hovering a type or member in code explains: its declaration, highlighted as code
/// with short names, the type or namespace it is in, null for a type in the global namespace, and
/// its XML documentation when there is any.</summary>
public sealed record CodeTooltip(CodeSignature Signature, CodeContainer? Container, IReadOnlyList<DocSection> Sections);
