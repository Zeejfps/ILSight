using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Shows the members whose code uses any accessor of a property or event, each once.</summary>
internal static class MemberUsedByAnalyzer
{
    public static AnalysisRun? For(ISymbol? symbol)
    {
        if (symbol is not IMember member || member is not (IProperty or IEvent))
            return null;
        if (!Members.Accessors(member).Any())
            return null;
        return context => Analyze(member, context);
    }

    private static IEnumerable<ISymbol> Analyze(IMember member, AnalyzerContext context)
    {
        var accessors = Members.Accessors(member);
        return accessors
            .SelectMany(a => MethodUsedByAnalyzer.Analyze(a, context))
            .Distinct();
    }
}
