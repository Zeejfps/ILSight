// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Shows the members and types a method's code, compiler-generated parts included, uses.</summary>
internal static class MethodUsesAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not IMethod method || !method.HasBody)
        {
            return null;
        }

        return context => Analyze(method, context);
    }

    private static IEnumerable<ISymbol> Analyze(IMethod method, AnalyzerContext context)
    {
        if (method.ParentModule?.MetadataFile is null)
        {
            throw new InvalidOperationException("Should never happen.");
        }

        var file = method.ParentModule.MetadataFile;
        var module = AssemblyListing.EnsureTypeSystemOf(context.AssemblyList, file).MainModule;
        return context.EnsureMapping(method).GetMethodParts(DecompilerFacts.GetMethodHandle(method))
            .SelectMany(part => ScanMethod(module, part)).Distinct();
    }

    private static IEnumerable<IEntity> ScanMethod(MetadataModule module, MethodDefinitionHandle handle)
    {
        var body = MethodBodies.Read(module.MetadataFile, handle);
        if (body is null)
        {
            yield break;
        }

        var types = new TypeDefinitionCollector();
        foreach (var (opCode, token) in MethodBodies.ListTokens(body))
        {
            var operandType = opCode.GetOperandType();
            if (operandType == OperandType.Type)
            {
                continue;
            }

            switch (Resolve(module, token))
            {
                case IType type:
                    type.AcceptVisitor(types);
                    break;
                case IEntity entity:
                    yield return entity;
                    break;
            }
        }
        foreach (var type in types.UsedTypes)
        {
            yield return type;
        }
    }

    private static object? Resolve(MetadataModule module, EntityHandle token)
    {
        try
        {
            return token.Kind switch
            {
                HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification => module.ResolveType(token, default),
                HandleKind.MethodDefinition or HandleKind.MethodSpecification or HandleKind.MemberReference or HandleKind.FieldDefinition =>
                    module.ResolveEntity(token, default),
                _ => null,
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }
}
