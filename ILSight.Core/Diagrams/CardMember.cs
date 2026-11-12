namespace ILSight.Core.Diagrams;

/// <summary>A member a card lists: what it points at, how the tree labels it, the kind its icon
/// shows, and its declared accessibility.</summary>
public sealed record CardMember(CodeReference.Entity Reference, string Text, MemberShape Shape, NodeAccessibility Accessibility)
{
    /// <summary>Whether code of another assembly can reach the member: it is public or protected.</summary>
    public bool IsVisibleOutside => Accessibility is NodeAccessibility.Public or NodeAccessibility.Protected or NodeAccessibility.ProtectedInternal;
}
