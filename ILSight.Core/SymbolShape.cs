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
}
