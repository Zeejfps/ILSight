// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Shows the members and types a method's code, compiler-generated parts included, uses.</summary>
/// <remarks>Each step reads under a catch of its own, noting a part the library fails reading as passed over: the
/// method's module and token, its parts, each part's body and instructions, then what an instruction chosen by its
/// operand names (a member, or the type definitions a type is made of). Which instructions are looked at, and each use
/// once, are decided outside.</remarks>
internal static class MethodUsesAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not IMethod method)
        {
            return null;
        }

        return context => Analyze(method, context);
    }

    /// <summary>A method with a body.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.Sort == AnalyzedSort.Method && facts.HasBody;
    }

    private static IEnumerable<ISymbol> Analyze(IMethod method, AnalyzerContext context)
    {
        var facts = ReadAnalyzed(method, context);
        if (facts is null)
        {
            return [];
        }

        var typeSystem = context.TypeSystems.Read.GetValueOrDefault(facts.File)
            ?? throw new InvalidOperationException($"The type system of {facts.File.FileName}, the analyzed method's, was not read before the analysis.");
        var main = ReadMainModule(typeSystem, facts.File, context.PassedOver);
        if (main is null)
        {
            return [];
        }

        var parts = MethodBodies.ReadParts(context, facts.File, facts.Token, (MethodDefinitionHandle)facts.Token);
        return (parts ?? []).SelectMany(part => ScanMethod(main, facts.File, part, context.PassedOver)).Distinct();
    }

    // The file and token of the analyzed method; null when the library fails reading them, noted.
    private static AnalyzedFacts? ReadAnalyzed(IMethod method, AnalyzerContext context)
    {
        MetadataFile? file;
        EntityHandle token;
        try
        {
            file = method.ParentModule?.MetadataFile;
            token = method.MetadataToken;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }

        if (file is null)
        {
            throw new InvalidOperationException("Should never happen.");
        }

        return new AnalyzedFacts { File = file, Token = token };
    }

    private sealed class AnalyzedFacts
    {
        public required MetadataFile File { get; init; }

        public required EntityHandle Token { get; init; }
    }

    private static MetadataModule? ReadMainModule(DecompilerTypeSystem typeSystem, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return typeSystem.MainModule;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static IEnumerable<IEntity> ScanMethod(MetadataModule module, MetadataFile file, MethodDefinitionHandle handle, PassedOver passedOver)
    {
        var body = MethodBodies.Read(file, handle, passedOver);
        if (body is null)
        {
            yield break;
        }

        var types = new TypeDefinitionCollector();
        foreach (var (opCode, token) in MethodBodies.ListTokens(file, body, passedOver))
        {
            var operandType = opCode.GetOperandType();
            if (operandType == OperandType.Type)
            {
                continue;
            }

            var entity = ReadUse(module, file, token, types, passedOver);
            if (entity is not null)
            {
                yield return entity;
            }
        }
        foreach (var type in types.UsedTypes)
        {
            yield return type;
        }
    }

    // The member the token names; for a type, null, the type definitions it is made of collected into types; null too
    // for anything else, or when the library fails resolving it, noted: what it names isn't listed.
    private static IEntity? ReadUse(MetadataModule module, MetadataFile file, EntityHandle token, TypeDefinitionCollector types, PassedOver passedOver)
    {
        try
        {
            object? resolved = token.Kind switch
            {
                HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification => module.ResolveType(token, default),
                HandleKind.MethodDefinition or HandleKind.MethodSpecification or HandleKind.MemberReference or HandleKind.FieldDefinition =>
                    module.ResolveEntity(token, default),
                _ => null,
            };
            if (resolved is IType type)
            {
                type.AcceptVisitor(types);
                return null;
            }

            return resolved as IEntity;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }
}
