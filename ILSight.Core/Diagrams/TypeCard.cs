namespace ILSight.Core.Diagrams;

/// <summary>A type as a diagram shows it: its name as the tree writes it, the kind its icon shows and
/// its stereotype; its own members by kind, then what it inherits by base type, nearest first; the links
/// from it to the types it derives from, implements and holds in its properties; and whether anything
/// can derive from it. <see cref="AssemblyPath"/> is the file of the assembly that defines it, and
/// <see cref="Anchor"/> names the type so that it is found again in another run.</summary>
public sealed record TypeCard(
    CodeReference.Entity Type,
    TypeAnchor Anchor,
    string Name,
    string FullName,
    AssemblyPath AssemblyPath,
    TypeShape Shape,
    NodeAccessibility Accessibility,
    TypeStereotype Stereotype,
    IReadOnlyList<CardSection> Sections,
    IReadOnlyList<InheritedGroup> Inherited,
    IReadOnlyList<TypeLink> Links,
    bool CanHaveDerived);
