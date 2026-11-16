// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Finds the visible members whose signature exposes a type.</summary>
/// <remarks>Each step reads under a catch of its own, noting a part the library fails reading as passed over: a type's
/// facts (whether it is hidden, its members' visibility), then the types the signature of each member ILSpy's test
/// looks in names. The tests run outside them.</remarks>
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

    /// <summary>Any type.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.Sort == AnalyzedSort.Type;
    }

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition analyzed, AnalyzerContext context)
    {
        var facts = ReadAnalyzed(analyzed, context);
        if (facts is null)
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
            var read = ReadType(type, file, context.PassedOver);
            if (read is null || facts.IsEnum && read.Key.Is(facts.Key) || read.IsHidden)
            {
                continue;
            }

            foreach (var member in read.Members)
            {
                if (!IsVisible(member))
                {
                    continue;
                }

                var named = ReadNamed(member.Member, file, context.PassedOver);
                if (named is not null && named.Any(n => n.Is(facts.Key)))
                {
                    yield return member.Member;
                }
            }
        }
    }

    // ILSpy's test of whose signature is looked in: a member not private, or an explicit implementation.
    private static bool IsVisible(MemberVisibility member)
    {
        return !member.IsPrivate || member.IsExplicitInterfaceImplementation;
    }

    // The analyzed type's definition and whether it is an enum; null when the library fails reading them, noted.
    private static AnalyzedFacts? ReadAnalyzed(ITypeDefinition analyzed, AnalyzerContext context)
    {
        try
        {
            return new AnalyzedFacts { Key = DefinitionKey.Of(analyzed), IsEnum = analyzed.Kind == TypeKind.Enum };
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

        public required bool IsEnum { get; init; }
    }

    // The type's definition, whether the library hides it, and each field, property, event and method with its
    // visibility; null when the library fails reading them, noted.
    private static TypeRead? ReadType(ITypeDefinition type, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            var members = type.Fields.Concat<IMember>(type.Properties).Concat(type.Events).Concat(type.Methods);
            return new TypeRead
            {
                Key = DefinitionKey.Of(type),
                IsHidden = Members.IsHidden(type),
                Members = members.Select(static m => new MemberVisibility
                {
                    Member = m, IsPrivate = m.Accessibility == Accessibility.Private, IsExplicitInterfaceImplementation = m.IsExplicitInterfaceImplementation,
                }).ToArray(),
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private sealed class TypeRead
    {
        public required DefinitionKey Key { get; init; }

        public required bool IsHidden { get; init; }

        public required MemberVisibility[] Members { get; init; }
    }

    private sealed class MemberVisibility
    {
        public required IMember Member { get; init; }

        public required bool IsPrivate { get; init; }

        public required bool IsExplicitInterfaceImplementation { get; init; }
    }

    // The type definitions the member's signature names; null when the library fails reading them, noted.
    private static IReadOnlyList<DefinitionKey>? ReadNamed(IMember member, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return ExposedTypeCollector.Collect(member);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }
}
