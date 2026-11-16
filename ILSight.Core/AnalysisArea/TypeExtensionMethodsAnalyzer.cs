// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Finds all extension methods defined for a type.</summary>
internal static class TypeExtensionMethodsAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not ITypeDefinition type)
        {
            return null;
        }

        return context => Analyze(type, context);
    }

    /// <summary>A type that isn't static.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.Sort == AnalyzedSort.Type && !facts.IsStatic;
    }

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition analyzed, AnalyzerContext context)
    {
        if (analyzed.ParentModule?.MetadataFile is null)
        {
            yield break;
        }

        foreach (var type in AnalyzerScope.Create(context, analyzed).GetTypes(context.CancellationToken))
        {
            if (!type.HasExtensions)
            {
                continue;
            }

            foreach (var method in type.Methods)
            {
                if (!method.IsExtensionMethod)
                {
                    continue;
                }

                var extended = method.Parameters[0].Type.GetDefinition();
                if (extended is not null && Members.IsSame(extended, analyzed))
                {
                    yield return method;
                }
            }
        }
    }
}
