// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Finds the members whose code, compiler-generated parts included, accesses a field in
/// one particular way.</summary>
/// <remarks>Each step reads under a catch of its own, noting a part the library fails reading as passed over: where a
/// method's code is, its parts, each part's body and instructions, then the field an access chosen by its opcode names.
/// Which opcodes access the field the way asked, and whether the field named is the analyzed one, are decided
/// outside.</remarks>
internal static class FieldAccessAnalyzer
{
    public static AnalysisRun? Prepare(FieldAccessKind kind, ISymbol? symbol)
    {
        if (symbol is not IField field)
        {
            return null;
        }

        return context => Analyze(kind, field, context);
    }

    /// <summary>A field; only read, when it is a constant.</summary>
    public static bool Applies(FieldAccessKind kind, OfferFacts facts)
    {
        return facts.Sort == AnalyzedSort.Field && (kind == FieldAccessKind.Read || !facts.IsConst);
    }

    private static IEnumerable<ISymbol> Analyze(FieldAccessKind kind, IField field, AnalyzerContext context)
    {
        var key = ReadKey(field, context);
        if (key is null)
        {
            yield break;
        }

        var scope = AnalyzerScope.Create(context, field);
        if (scope is null)
        {
            yield break;
        }

        foreach (var member in scope.FindMembersWithCode((file, method) => Accesses(kind, file, method, key.Value, context), context.CancellationToken))
        {
            yield return member;
        }
    }

    private static DefinitionKey? ReadKey(IField field, AnalyzerContext context)
    {
        try
        {
            return DefinitionKey.Of(field);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }
    }

    private static bool Accesses(FieldAccessKind kind, MetadataFile file, IMethod method, DefinitionKey field, AnalyzerContext context)
    {
        var code = MethodBodies.ReadCode(method, file, context.PassedOver);
        if (code is null)
        {
            return false;
        }

        var parts = MethodBodies.ReadParts(context, code.File, code.Declaring, code.Handle);
        return parts is not null && parts.Any(part => AccessesIn(kind, code, part, field, context.PassedOver));
    }

    private static bool AccessesIn(FieldAccessKind kind, MethodCode code, MethodDefinitionHandle part, DefinitionKey field, PassedOver passedOver)
    {
        var body = MethodBodies.Read(code.File, part, passedOver);
        if (body is null)
        {
            return false;
        }

        foreach (var (opCode, token) in MethodBodies.ListTokens(code.File, body, passedOver))
        {
            if (!Is(kind, opCode) || !token.Kind.IsMemberKind())
            {
                continue;
            }

            var found = ReadField(code, token, passedOver);
            if (found is not null && found.Value.Is(field))
            {
                return true;
            }
        }
        return false;
    }

    // The field the token names; null when it names none, or the library fails resolving it, noted.
    private static DefinitionKey? ReadField(MethodCode code, EntityHandle token, PassedOver passedOver)
    {
        try
        {
            var found = code.Module.ResolveEntity(token);
            return found is IField field ? DefinitionKey.Of(field) : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(code.File, e);
            return null;
        }
    }

    private static bool Is(FieldAccessKind kind, ILOpCode opCode)
    {
        return opCode switch
        {
            ILOpCode.Ldfld or ILOpCode.Ldsfld => kind == FieldAccessKind.Read,
            ILOpCode.Stfld or ILOpCode.Stsfld => kind == FieldAccessKind.Write,
            ILOpCode.Ldflda or ILOpCode.Ldsflda => kind == FieldAccessKind.AddressOf,
            _ => false,
        };
    }
}
