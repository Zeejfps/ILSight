namespace ILSight.Core.Diagrams;

/// <summary>A card's own members of one kind, sorted as the tree sorts them.</summary>
public sealed record CardSection(MemberSection Kind, IReadOnlyList<CardMember> Members);
