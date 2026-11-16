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

        return context => Analyze(member, context);
    }

    /// <summary>An instance event, indexer, method or property of a class or struct.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.SymbolKind is SymbolKind.Event or SymbolKind.Indexer or SymbolKind.Method or SymbolKind.Property
            && !facts.IsStatic
            && facts.DeclaringDefinitionKind is TypeKind.Class or TypeKind.Struct;
    }

    // The base members read with the kind of the type declaring each, under the read's catch; those an interface
    // declares chosen outside it.
    private static IEnumerable<ISymbol> Analyze(IMember member, AnalyzerContext context)
    {
        var baseMembers = ReadBaseMembers(member, context);
        return (baseMembers ?? [])
            .Where(static m => m.DeclaringKind == TypeKind.Interface)
            .Select(static m => m.Member);
    }

    // The members the member overrides or implements, by the library's walk, each with the kind of the type declaring
    // it; null when the library fails reading them, noted.
    private static (IMember Member, TypeKind? DeclaringKind)[]? ReadBaseMembers(IMember member, AnalyzerContext context)
    {
        try
        {
            return InheritanceHelper.GetBaseMembers(member, includeImplementedInterfaces: true)
                .Select(static m => (m, m.DeclaringTypeDefinition?.Kind))
                .ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }
    }
}
