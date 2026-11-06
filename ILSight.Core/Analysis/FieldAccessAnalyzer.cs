// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

internal enum FieldAccessKind
{
    Read,
    Write,
    AddressOf,
}

/// <summary>Finds the members whose code, compiler-generated parts included, accesses a field in
/// one particular way.</summary>
internal static class FieldAccessAnalyzer
{
    public static AnalysisRun? Prepare(FieldAccessKind kind, ISymbol? symbol)
    {
        if (symbol is not IField field || (kind != FieldAccessKind.Read && field.IsConst))
            return null;
        return context => Analyze(kind, field, context);
    }

    private static IEnumerable<ISymbol> Analyze(FieldAccessKind kind, IField field, AnalyzerContext context)
    {
        return context.GetScopeOf(field).MembersWhoseCode(method => Accesses(kind, method, field, context), context.CancellationToken);
    }

    private static bool Accesses(FieldAccessKind kind, IMethod method, IField field, AnalyzerContext context)
    {
        if (method.MetadataToken.IsNil || method.ParentModule is not MetadataModule module || module.MetadataFile == null)
            return false;
        var file = module.MetadataFile;
        return context.MappingOf(method.DeclaringTypeDefinition!).GetMethodParts((MethodDefinitionHandle)method.MetadataToken)
            .Any(part => file.ReadBody(part).AnyToken((opCode, token) =>
                Is(kind, opCode) && token.Kind.IsMemberKind() && module.ResolveEntity(token) is IField found && found.IsSame(field)));
    }

    private static bool Is(FieldAccessKind kind, ILOpCode opCode) =>
        opCode switch
        {
            ILOpCode.Ldfld or ILOpCode.Ldsfld => kind == FieldAccessKind.Read,
            ILOpCode.Stfld or ILOpCode.Stsfld => kind == FieldAccessKind.Write,
            ILOpCode.Ldflda or ILOpCode.Ldsflda => kind == FieldAccessKind.AddressOf,
            _ => false,
        };
}
