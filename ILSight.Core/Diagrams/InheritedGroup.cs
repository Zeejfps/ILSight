namespace ILSight.Core.Diagrams;

/// <summary>The members a card's type inherits from one of its base types, which is named
/// <see cref="From"/> as the type derives from it and defined in the assembly at <see cref="AssemblyPath"/>.</summary>
public sealed record InheritedGroup
{
    public required string From { get; init; }

    public required AssemblyPath? AssemblyPath { get; init; }

    public required IReadOnlyList<CardMember> Members { get; init; }
}
