namespace ILSight.Core.Diagrams;

/// <summary>How one type of a diagram relates to another.</summary>
public enum TypeLinkKind
{
    /// <summary>Derives from the class, or extends the interface.</summary>
    Inherits,

    /// <summary>Implements the interface.</summary>
    Implements,

    /// <summary>Has a property holding one of the type.</summary>
    HoldsOne,

    /// <summary>Has a property holding many of the type.</summary>
    HoldsMany,
}
