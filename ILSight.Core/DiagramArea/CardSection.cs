namespace ILSight.Core.DiagramArea;

/// <summary>A card's own members of one kind, sorted as the tree sorts them.</summary>
public sealed record CardSection
{
    public required MemberSection Kind { get; init; }

    public required IReadOnlyList<CardMember> Members { get; init; }
}
