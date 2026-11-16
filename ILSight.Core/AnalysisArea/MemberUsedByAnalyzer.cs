using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

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

    // The accessors read under the read's catch; the users of each found by the method analysis, each once.
    private static IEnumerable<ISymbol> Analyze(IMember member, AnalyzerContext context)
    {
        var accessors = ReadAccessors(member, context);
        return (accessors ?? [])
            .SelectMany(a => MethodUsedByAnalyzer.Analyze(a, context))
            .Distinct();
    }

    // The member's accessors; null when the library fails reading them, noted.
    private static IMethod[]? ReadAccessors(IMember member, AnalyzerContext context)
    {
        try
        {
            return Members.Accessors(member).ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }
    }
}
