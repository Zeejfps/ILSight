using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Shows the members of kind <typeparamref name="T"/> that implement an interface member,
/// or with <c>overrides</c> those that override a class member.</summary>
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
        var declaring = analyzed.DeclaringTypeDefinition;
        if (declaring is null || declaring.ParentModule?.MetadataFile is null)
        {
            yield break;
        }

        foreach (var type in AnalyzerScope.Create(context, analyzed).GetTypes(context.CancellationToken))
        {
            var baseTypes = type.GetAllBaseTypeDefinitions();
            if (!baseTypes.Any(t => Members.IsSame(t, declaring)))
            {
                continue;
            }

            foreach (var member in type.Members.OfType<T>())
            {
                if (overrides && !member.IsOverride)
                {
                    continue;
                }

                var baseMembers = InheritanceHelper.GetBaseMembers(member, !overrides);
                if (baseMembers.Any(m => Members.IsSame(m, analyzed)))
                {
                    yield return member;
                }
            }
        }
    }
}
