// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Shows the members whose code instantiates a type.</summary>
/// <remarks>Each step reads under a catch of its own, noting a part the library fails reading as passed over: where a
/// method's code is, its body and instructions, then the type an instruction chosen by its opcode constructs. Which
/// opcodes instantiate, and whether the type constructed is the analyzed one, are decided outside.</remarks>
internal static class TypeInstantiatedByAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not ITypeDefinition type)
        {
            return null;
        }

        return context => Analyze(type, context);
    }

    /// <summary>A type neither abstract nor static.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.Sort == AnalyzedSort.Type && !facts.IsAbstract && !facts.IsStatic;
    }

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition type, AnalyzerContext context)
    {
        var facts = ReadAnalyzed(type, context);
        if (facts is null)
        {
            yield break;
        }

        var scope = AnalyzerScope.Create(context, type);
        if (scope is null)
        {
            yield break;
        }

        foreach (var member in scope.FindMembersWithCode((file, method) => Instantiates(file, method, facts, context.PassedOver), context.CancellationToken))
        {
            yield return member;
        }
    }

    // The analyzed type's definition and whether it is a struct; null when the library fails reading them, noted.
    private static AnalyzedFacts? ReadAnalyzed(ITypeDefinition type, AnalyzerContext context)
    {
        try
        {
            return new AnalyzedFacts { Key = DefinitionKey.Of(type), IsStruct = type.Kind == TypeKind.Struct };
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

        public required bool IsStruct { get; init; }
    }

    private static bool Instantiates(MetadataFile file, IMethod method, AnalyzedFacts type, PassedOver passedOver)
    {
        var code = MethodBodies.ReadCode(method, file, passedOver);
        if (code is null || !code.HasBody)
        {
            return false;
        }

        var body = MethodBodies.Read(code.File, code.Handle, passedOver);
        if (body is null)
        {
            return false;
        }

        foreach (var (opCode, token) in MethodBodies.ListTokens(code.File, body, passedOver))
        {
            if (!IsInstantiation(opCode, type.IsStruct))
            {
                continue;
            }

            var found = ReadConstructed(code, token, passedOver);
            if (found is not null && found.Value.Is(type.Key))
            {
                return true;
            }
        }
        return false;
    }

    // ILSpy's test: a new object, an initialised value, or a call to a struct's constructor.
    private static bool IsInstantiation(ILOpCode opCode, bool isStruct)
    {
        return opCode is ILOpCode.Newobj or ILOpCode.Initobj || opCode == ILOpCode.Call && isStruct;
    }

    // The type definition, of a module, the token constructs: the type of the constructor it calls, or the type it
    // names; null for anything else, or when the library fails resolving it, noted.
    private static DefinitionKey? ReadConstructed(MethodCode code, EntityHandle token, PassedOver passedOver)
    {
        try
        {
            var found = token.Kind switch
            {
                HandleKind.MethodDefinition or HandleKind.MemberReference => FindConstructedType(code.Module, token),
                HandleKind.TypeDefinition or HandleKind.TypeReference => code.Module.ResolveType(token, default)?.GetDefinition(),
                _ => null,
            };
            return found?.ParentModule is not null ? DefinitionKey.Of(found) : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(code.File, e);
            return null;
        }
    }

    private static ITypeDefinition? FindConstructedType(MetadataModule module, EntityHandle token)
    {
        var method = module.ResolveMethod(token, default);
        if (method is null || !method.IsConstructor)
        {
            return null;
        }

        return method.DeclaringTypeDefinition;
    }
}
