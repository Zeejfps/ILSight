// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Finds all extension methods defined for a type.</summary>
internal static class TypeExtensionMethodsAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not ITypeDefinition type || type.IsStatic)
        {
            return null;
        }

        return context => Analyze(type, context);
    }

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition analyzed, AnalyzerContext context)
    {
        if (analyzed.ParentModule?.MetadataFile is null)
        {
            yield break;
        }

        foreach (var type in AnalyzerHelpers.GetTypesInScope(AnalyzerHelpers.GetScope(context, analyzed), context.CancellationToken))
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
                if (extended is not null && AnalyzerHelpers.IsSame(extended, analyzed))
                {
                    yield return method;
                }
            }
        }
    }
}
