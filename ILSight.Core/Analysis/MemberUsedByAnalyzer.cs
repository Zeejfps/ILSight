using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Shows the members whose code uses any accessor of a property or event, each once.</summary>
internal static class MemberUsedByAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not IMember member || member is not (IProperty or IEvent))
        {
            return null;
        }

        return context => Analyze(member, context);
    }

    /// <summary>A property or event with an accessor.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.Sort is AnalyzedSort.Property or AnalyzedSort.Event && facts.HasAccessors;
    }

    private static IEnumerable<ISymbol> Analyze(IMember member, AnalyzerContext context)
    {
        var accessors = Members.Accessors(member);
        return accessors
            .SelectMany(a => MethodUsedByAnalyzer.Analyze(a, context))
            .Distinct();
    }
}
