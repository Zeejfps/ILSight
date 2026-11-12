namespace ILSight.Core.Diagrams;

/// <summary>A link drawn from the type <see cref="From"/> to the type <see cref="To"/>, labelled with the
/// name of the property that holds it, for the links that are held.</summary>
public sealed record TypeLink(CodeReference.Entity From, CodeReference.Entity To, TypeLinkKind Kind, string? Label = null);
