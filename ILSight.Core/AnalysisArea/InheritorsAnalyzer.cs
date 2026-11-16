using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Shows the members of kind <typeparamref name="T"/> that implement an interface member,
/// or with <c>overrides</c> those that override a class member.</summary>
/// <remarks>Each step reads the facts its test needs under a catch of its own (the analyzed member's, then each type's
/// bases, then each of its members' bases), noting a part the library fails reading as passed over; the tests run
/// outside them.</remarks>
internal static class InheritorsAnalyzer<T> where T : class, IMember
{
    public static AnalysisRun? Prepare(bool overrides, ISymbol? symbol)
    {
        if (symbol is not T member)
        {
            return null;
        }

        return context => Analyze(overrides, member, context);
    }

    /// <summary>A member of <paramref name="sort"/>: overridden, when overridable outside an interface; implemented,
    /// when declared by an interface.</summary>
    public static bool Applies(bool overrides, AnalyzedSort sort, OfferFacts facts)
    {
        if (facts.Sort != sort)
        {
            return false;
        }

        return overrides
        ? facts.IsOverridable && facts.DeclaringTypeKind != TypeKind.Interface
        : facts.DeclaringTypeKind == TypeKind.Interface;
    }

    private static IEnumerable<ISymbol> Analyze(bool overrides, T analyzed, AnalyzerContext context)
    {
        var facts = ReadAnalyzed(analyzed, context);
        if (facts is null || !facts.HasDeclaringFile)
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
            var bases = ReadBaseDefinitions(type, file, context.PassedOver);
            if (bases is null || !bases.Any(b => b.Is(facts.Declaring)))
            {
                continue;
            }

            var members = ReadMembers(type, file, context.PassedOver);
            foreach (var member in members ?? [])
            {
                if (overrides && !member.IsOverride)
                {
                    continue;
                }

                var baseMembers = ReadBaseMembers(member.Member, overrides, file, context.PassedOver);
                if (baseMembers is not null && baseMembers.Any(m => m.Is(facts.Key)))
                {
                    yield return member.Member;
                }
            }
        }
    }

    // The analyzed member's definition and the type declaring it, and whether that type was read from a file; null when
    // the library fails reading them, or no type declares the member.
    private static AnalyzedFacts? ReadAnalyzed(T analyzed, AnalyzerContext context)
    {
        try
        {
            var declaring = analyzed.DeclaringTypeDefinition;
            if (declaring is null)
            {
                return null;
            }

            return new AnalyzedFacts
            {
                Key = DefinitionKey.Of(analyzed), Declaring = DefinitionKey.Of(declaring), HasDeclaringFile = declaring.ParentModule?.MetadataFile is not null,
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }
    }

    private sealed class AnalyzedFacts
    {
        public required DefinitionKey Key { get; init; }

        public required DefinitionKey Declaring { get; init; }

        public required bool HasDeclaringFile { get; init; }
    }

    // Every type the type derives from or implements, by the library's walk; null when it fails, noted.
    private static DefinitionKey[]? ReadBaseDefinitions(ITypeDefinition type, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return type.GetAllBaseTypeDefinitions().Select(DefinitionKey.Of).ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // The type's members of kind T, each with whether it overrides; null when the library fails reading them, noted.
    private static (T Member, bool IsOverride)[]? ReadMembers(ITypeDefinition type, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return type.Members.OfType<T>().Select(static m => (m, m.IsOverride)).ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // The members the member overrides, and with implementations the interface members it implements, by the library's
    // walk; null when it fails, noted.
    private static DefinitionKey[]? ReadBaseMembers(T member, bool overrides, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return InheritanceHelper.GetBaseMembers(member, !overrides).Select(DefinitionKey.Of).ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }
}
