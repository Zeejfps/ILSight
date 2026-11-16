// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Finds all extension methods defined for a type.</summary>
/// <remarks>Each type's extension methods are read with the definition each extends under a catch of their own, noting
/// a type the library fails reading as passed over; which extend the analyzed type is decided outside it.</remarks>
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
        var key = ReadAnalyzed(analyzed, context);
        if (key is null)
        {
            yield break;
        }

        var scope = AnalyzerScope.Create(context, analyzed);
        if (scope is null)
        {
            yield break;
        }

        foreach (var (file, type) in scope.ListTypes(context.CancellationToken))
        {
            var extensions = ReadExtensions(type, file, context.PassedOver);
            foreach (var (method, extended) in extensions ?? [])
            {
                if (extended is not null && extended.Value.Is(key.Value))
                {
                    yield return method;
                }
            }
        }
    }

    // The analyzed type's definition; null when it was read from no file, or the library fails reading it, noted.
    private static DefinitionKey? ReadAnalyzed(ITypeDefinition analyzed, AnalyzerContext context)
    {
        try
        {
            return analyzed.ParentModule?.MetadataFile is null ? null : DefinitionKey.Of(analyzed);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }
    }

    // The type's extension methods, when the library says it has any, each with the definition of the type its first
    // parameter takes, null for one with none; null when the library fails reading them, noted.
    private static (IMethod Method, DefinitionKey? Extended)[]? ReadExtensions(ITypeDefinition type, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            if (!type.HasExtensions)
            {
                return [];
            }

            return type.Methods
                .Where(static m => m.IsExtensionMethod)
                .Select(static m => (m, ReadExtended(m)))
                .ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static DefinitionKey? ReadExtended(IMethod method)
    {
        var definition = method.Parameters[0].Type.GetDefinition();
        return definition is null ? null : DefinitionKey.Of(definition);
    }
}
