using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Shows the interface members a class or struct member implements.</summary>
internal static class MemberImplementsInterfaceAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not IMember member)
        {
            return null;
        }

        return _ => Analyze(member);
    }

    /// <summary>An instance event, indexer, method or property of a class or struct.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.SymbolKind is SymbolKind.Event or SymbolKind.Indexer or SymbolKind.Method or SymbolKind.Property
            && !facts.IsStatic
            && facts.DeclaringDefinitionKind is TypeKind.Class or TypeKind.Struct;
    }

    private static IEnumerable<ISymbol> Analyze(IMember member)
    {
        return InheritanceHelper.GetBaseMembers(member, includeImplementedInterfaces: true)
            .Where(m => m.DeclaringTypeDefinition?.Kind == TypeKind.Interface);
    }
}
