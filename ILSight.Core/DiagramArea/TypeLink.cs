namespace ILSight.Core.DiagramArea;

/// <summary>A link drawn from the type <see cref="From"/> to the type <see cref="To"/>, by how the one
/// relates to the other.</summary>
public union TypeLink(TypeLink.Inherits, TypeLink.Implements, TypeLink.HoldsOne, TypeLink.HoldsMany)
{
    /// <summary>Derives from the class, or extends the interface.</summary>
    public sealed record Inherits
    {
        public required CodeReference.Entity From { get; init; }

        public required CodeReference.Entity To { get; init; }
    }

    /// <summary>Implements the interface.</summary>
    public sealed record Implements
    {
        public required CodeReference.Entity From { get; init; }

        public required CodeReference.Entity To { get; init; }
    }

    /// <summary>Has the property <see cref="Property"/> holding one of the type.</summary>
    public sealed record HoldsOne
    {
        public required CodeReference.Entity From { get; init; }

        public required CodeReference.Entity To { get; init; }

        public required string Property { get; init; }
    }

    /// <summary>Has the property <see cref="Property"/> holding many of the type.</summary>
    public sealed record HoldsMany
    {
        public required CodeReference.Entity From { get; init; }

        public required CodeReference.Entity To { get; init; }

        public required string Property { get; init; }
    }

    /// <summary>The type the link is drawn from.</summary>
    public CodeReference.Entity From => this switch
    {
        Inherits inherits => inherits.From,
        Implements implements => implements.From,
        HoldsOne holdsOne => holdsOne.From,
        HoldsMany holdsMany => holdsMany.From,
    };

    /// <summary>The type the link is drawn to.</summary>
    public CodeReference.Entity To => this switch
    {
        Inherits inherits => inherits.To,
        Implements implements => implements.To,
        HoldsOne holdsOne => holdsOne.To,
        HoldsMany holdsMany => holdsMany.To,
    };

    /// <summary>Whether <see cref="From"/> holds <see cref="To"/> in a property, rather than being one.</summary>
    public bool IsHeld => this switch
    {
        Inherits or Implements => false,
        HoldsOne or HoldsMany => true,
    };
}
