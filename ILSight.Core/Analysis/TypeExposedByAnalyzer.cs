// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Finds the visible members whose signature exposes a type.</summary>
internal static class TypeExposedByAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not ITypeDefinition type)
        {
            return null;
        }

        return context => Analyze(type, context);
    }

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition analyzed, AnalyzerContext context)
    {
        var visitor = new TypeDefinitionUsedVisitor(analyzed);
        foreach (var type in AnalyzerScope.Create(context.AssemblyList, analyzed).GetTypes(context.CancellationToken))
        {
            if (analyzed.Kind == TypeKind.Enum && Members.IsSame(type, analyzed) || Members.IsHidden(type))
            {
                continue;
            }

            foreach (var member in type.Fields.Concat<IMember>(type.Properties).Concat(type.Events).Concat(type.Methods))
            {
                if (visitor.IsExposedBy(member))
                {
                    yield return member;
                }
            }
        }
    }
}
