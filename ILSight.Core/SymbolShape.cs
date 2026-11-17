namespace ILSight.Core;

/// <summary>What a symbol an API comparison, an analysis or a diagram lists is: an assembly, a namespace,
/// or a type or member of some shape.</summary>
public union SymbolShape(SymbolShape.Assembly, SymbolShape.Namespace, SymbolShape.Type, SymbolShape.Member)
{
    public sealed record Assembly;

    public sealed record Namespace;

    public sealed record Type(TypeShape Shape);

    public sealed record Member(MemberShape Shape);
}
