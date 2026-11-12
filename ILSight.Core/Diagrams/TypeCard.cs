namespace ILSight.Core.Diagrams;

/// <summary>A type as a diagram shows it: its name as the tree writes it, the kind its icon shows and
/// its stereotype; its own members by kind, then what it inherits by base type, nearest first; the links
/// from it to the types it derives from, implements and holds in its properties; and whether anything
/// can derive from it. <see cref="AssemblyPath"/> is the file of the assembly that defines it, and
/// <see cref="Anchor"/> names the type so that it is found again in another run.</summary>
public sealed record TypeCard
{
    public required CodeReference.Entity Type { get; init; }

    public required TypeAnchor Anchor { get; init; }

    public required string Name { get; init; }

    public required string FullName { get; init; }

    public required AssemblyPath AssemblyPath { get; init; }

    public required TypeShape Shape { get; init; }

    public required NodeAccessibility Accessibility { get; init; }

    public required TypeStereotype Stereotype { get; init; }

    public required IReadOnlyList<CardSection> Sections { get; init; }

    public required IReadOnlyList<InheritedGroup> Inherited { get; init; }

    public required IReadOnlyList<TypeLink> Links { get; init; }

    public required bool CanHaveDerived { get; init; }
}
