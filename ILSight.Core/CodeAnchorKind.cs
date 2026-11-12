namespace ILSight.Core;

/// <summary>What a <see cref="CodeAnchor"/> names.</summary>
public enum CodeAnchorKind
{
    /// <summary>The line defining an entity: a type, method, field, property or event.</summary>
    Definition,

    /// <summary>The line where the statement at an IL offset of a method starts.</summary>
    Statement,
}
