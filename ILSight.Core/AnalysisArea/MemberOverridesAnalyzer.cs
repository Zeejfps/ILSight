using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Shows the base members a member overrides, up its override chain.</summary>
internal static class MemberOverridesAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not IMember member)
        {
            return null;
        }

        return context => Analyze(member, context);
    }

    /// <summary>An event, indexer, method or property that overrides.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.SymbolKind is SymbolKind.Event or SymbolKind.Indexer or SymbolKind.Method or SymbolKind.Property && facts.IsOverride;
    }

    // Up the chain a step at a time: each step's member read under a catch of its own (whether it overrides, its
    // definition, the member it overrides), the walk's stop at a definition seen before decided outside it.
    private static IEnumerable<ISymbol> Analyze(IMember member, AnalyzerContext context)
    {
        var visited = new HashSet<IMember>();
        while (true)
        {
            var definition = ReadOverridingDefinition(member, context);
            if (definition is null || !visited.Add(definition))
            {
                break;
            }

            var baseMember = ReadBaseMember(definition, context);
            if (baseMember is null)
            {
                break;
            }

            yield return baseMember;
            member = baseMember;
        }
    }

    // The definition of the member, when it overrides; null when it doesn't, or the library fails reading it, noted.
    private static IMember? ReadOverridingDefinition(IMember member, AnalyzerContext context)
    {
        try
        {
            return member.IsOverride ? member.MemberDefinition : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }
    }

    // The member the definition overrides, by the library's walk; null when there is none, or the library fails reading
    // it, noted.
    private static IMember? ReadBaseMember(IMember definition, AnalyzerContext context)
    {
        try
        {
            return InheritanceHelper.GetBaseMember(definition);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }
    }
}
