using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Shows the interface members a class or struct member implements.</summary>
internal static class MemberImplementsInterfaceAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not IMember { SymbolKind: SymbolKind.Event or SymbolKind.Indexer or SymbolKind.Method or SymbolKind.Property, IsStatic: false } member
            || member.DeclaringTypeDefinition?.Kind is not (TypeKind.Class or TypeKind.Struct))
        {
            return null;
        }

        return _ => Analyze(member);
    }

    private static IEnumerable<ISymbol> Analyze(IMember member)
    {
        return InheritanceHelper.GetBaseMembers(member, includeImplementedInterfaces: true)
            .Where(m => m.DeclaringTypeDefinition?.Kind == TypeKind.Interface);
    }
}
