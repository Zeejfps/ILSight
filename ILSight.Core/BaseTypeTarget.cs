using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What a base type resolves to: a definition that can be navigated to, one that cannot, or none.</summary>
public abstract record BaseTypeTarget
{
    private BaseTypeTarget()
    {
    }

    public sealed record Navigable(CodeReference.Entity Reference) : BaseTypeTarget;

    public sealed record Unaddressed : BaseTypeTarget;

    public sealed record Unresolved : BaseTypeTarget;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Navigable, TResult> navigable, Func<Unaddressed, TResult> unaddressed,
        Func<Unresolved, TResult> unresolved)
    {
        return this switch
        {
            Navigable c => navigable(c),
            Unaddressed c => unaddressed(c),
            Unresolved c => unresolved(c),
            _ => throw new UnreachableException($"BaseTypeTarget.Match does not handle {GetType().Name}."),
        };
    }
}
