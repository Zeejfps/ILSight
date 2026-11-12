namespace ILSight.Core.Comparison;

/// <summary>A type or member as one assembly has it: what it points at, and how it is declared, as C# writes it.</summary>
public sealed record ApiSide(CodeReference.Entity Reference, string Declaration);
