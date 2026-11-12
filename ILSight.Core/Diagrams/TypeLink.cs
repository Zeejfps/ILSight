using System.Diagnostics;

namespace ILSight.Core.Diagrams;

/// <summary>A link drawn from the type <see cref="From"/> to the type <see cref="To"/>, by how the one
/// relates to the other.</summary>
public abstract record TypeLink
{
    private TypeLink()
    {
    }

    public required CodeReference.Entity From { get; init; }

    public required CodeReference.Entity To { get; init; }

    /// <summary>Whether <see cref="From"/> holds <see cref="To"/> in a property, rather than being one.</summary>
    public abstract bool IsHeld { get; }

    /// <summary>Derives from the class, or extends the interface.</summary>
    public sealed record Inherits : TypeLink
    {
        public override bool IsHeld => false;
    }

    /// <summary>Implements the interface.</summary>
    public sealed record Implements : TypeLink
    {
        public override bool IsHeld => false;
    }

    /// <summary>Has the property <see cref="Property"/> holding one of the type.</summary>
    public sealed record HoldsOne : TypeLink
    {
        public required string Property { get; init; }

        public override bool IsHeld => true;
    }

    /// <summary>Has the property <see cref="Property"/> holding many of the type.</summary>
    public sealed record HoldsMany : TypeLink
    {
        public required string Property { get; init; }

        public override bool IsHeld => true;
    }

    public TResult Match<TResult>(Func<Inherits, TResult> inherits, Func<Implements, TResult> implements, Func<HoldsOne, TResult> holdsOne,
        Func<HoldsMany, TResult> holdsMany)
    {
        return this switch
        {
            Inherits i => inherits(i),
            Implements i => implements(i),
            HoldsOne h => holdsOne(h),
            HoldsMany h => holdsMany(h),
            _ => throw new UnreachableException($"TypeLink.Match does not handle {GetType().Name}."),
        };
    }
}
