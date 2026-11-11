// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Shows the members whose code instantiates a type.</summary>
internal static class TypeInstantiatedByAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not ITypeDefinition type || type.IsAbstract || type.IsStatic)
        {
            return null;
        }

        return context => Analyze(type, context);
    }

    private static IEnumerable<ISymbol> Analyze(ITypeDefinition type, AnalyzerContext context)
    {
        return AnalyzerHelpers.FindMembersWithCode(AnalyzerHelpers.GetScope(context, type), 
            method => method.ParentModule is MetadataModule module
                && AnalyzerHelpers.HasToken(AnalyzerHelpers.FindMethodBody(context, method), (opCode, token) => Instantiates(module, opCode, token, type)),
            context.CancellationToken);
    }

    private static bool Instantiates(MetadataModule module, ILOpCode opCode, EntityHandle token, ITypeDefinition type)
    {
        if (opCode is not (ILOpCode.Newobj or ILOpCode.Initobj) && !(opCode == ILOpCode.Call && type.Kind == TypeKind.Struct))
        {
            return false;
        }

        var found = token.Kind switch
        {
            HandleKind.MethodDefinition or HandleKind.MemberReference => FindConstructedType(module, token),
            HandleKind.TypeDefinition or HandleKind.TypeReference => module.ResolveType(token, default)?.GetDefinition(),
            _ => null,
        };
        return found?.ParentModule is not null && AnalyzerHelpers.IsSame(found, type);
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
