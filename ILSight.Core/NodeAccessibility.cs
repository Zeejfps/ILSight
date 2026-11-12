namespace ILSight.Core;

/// <summary>The declared accessibility of the entity a <see cref="BrowseNode"/> stands for.</summary>
public enum NodeAccessibility
{
    None,
    Public,
    Protected,
    ProtectedInternal,
    Internal,
    PrivateProtected,
    Private,
}
