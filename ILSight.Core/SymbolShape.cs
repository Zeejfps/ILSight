using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What a symbol an API comparison, an analysis or a diagram lists is: an assembly, a namespace,
/// or a type or member of some shape.</summary>
public abstract record SymbolShape
{
    private SymbolShape()
    {
    }

    public sealed record Assembly : SymbolShape;

    public sealed record Namespace : SymbolShape;

    public sealed record Type(TypeShape Shape) : SymbolShape;

    public sealed record Member(MemberShape Shape) : SymbolShape;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Assembly, TResult> assembly, Func<Namespace, TResult> @namespace,
        Func<Type, TResult> type, Func<Member, TResult> member)
    {
        return this switch
        {
            Assembly c => assembly(c),
            Namespace c => @namespace(c),
            Type c => type(c),
            Member c => member(c),
            _ => throw new UnreachableException($"SymbolShape.Match does not handle {GetType().Name}."),
        };
    }
}
