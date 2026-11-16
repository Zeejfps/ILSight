// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Method bodies as the analyzers read them: where a method's code is, the methods C# made of it, the body
/// of each, and the metadata tokens its instructions name. Each is read under a catch of its own; what the library
/// can't read of them is noted in a <see cref="PassedOver"/>, so the analysis says the module partly read, and the
/// analyzers' tests run outside.</summary>
internal static class MethodBodies
{
    /// <summary>Where the code of <paramref name="method"/>, found in <paramref name="file"/>, is; null for a method
    /// with no token or of no module read from a file, or when the library fails reading it, noted in
    /// <paramref name="passedOver"/>.</summary>
    public static MethodCode? ReadCode(IMethod method, MetadataFile file, PassedOver passedOver)
    {
        try
        {
            if (method.MetadataToken.IsNil || method.ParentModule is not MetadataModule module || module.MetadataFile is null)
            {
                return null;
            }

            return new MethodCode
            {
                Module = module, File = module.MetadataFile, Token = method.MetadataToken, DeclaringType = method.DeclaringTypeDefinition?.MetadataToken,
                HasBody = method.HasBody,
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    /// <summary>The methods the code of method <paramref name="method"/> is made of, itself and those the compiler
    /// generated for it, by C#'s mapping of the code around the entity <paramref name="around"/> names, kept in
    /// <paramref name="context"/>; null when the library fails reading them, noted.</summary>
    public static MethodDefinitionHandle[]? ReadParts(AnalyzerContext context, MetadataFile file, EntityHandle around, MethodDefinitionHandle method)
    {
        var kept = context.FindMapping(file, around);
        CodeMappingInfo mapping;
        MethodDefinitionHandle[] parts;
        try
        {
            mapping = kept ?? CSharpDecompiler.GetCodeMappingInfo(file, around);
            parts = mapping.GetMethodParts(method).ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(file, e);
            return null;
        }

        context.KeepMapping(file, around, mapping);
        return parts;
    }

    /// <summary>The method whose code method <paramref name="method"/> is part of, itself unless the compiler generated
    /// it, by C#'s mapping of the code around the entity <paramref name="around"/> names, kept in
    /// <paramref name="context"/>; null when the library fails reading it, noted.</summary>
    public static MethodDefinitionHandle? ReadParent(AnalyzerContext context, MetadataFile file, EntityHandle around, MethodDefinitionHandle method)
    {
        var kept = context.FindMapping(file, around);
        CodeMappingInfo mapping;
        MethodDefinitionHandle parent;
        try
        {
            mapping = kept ?? CSharpDecompiler.GetCodeMappingInfo(file, around);
            parent = mapping.GetParentMethod(method);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(file, e);
            return null;
        }

        context.KeepMapping(file, around, mapping);
        return parent;
    }

    /// <summary>The body of method <paramref name="handle"/>; null when it has none, or it cannot be read, which is
    /// noted in <paramref name="passedOver"/>.</summary>
    public static MethodBodyBlock? Read(MetadataFile module, MethodDefinitionHandle handle, PassedOver passedOver)
    {
        try
        {
            var definition = module.Metadata.GetMethodDefinition(handle);
            return definition.HasBody() ? module.GetMethodBody(definition.RelativeVirtualAddress) : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(module, e);
            return null;
        }
    }

    /// <summary>The instructions of <paramref name="body"/>, of <paramref name="module"/>, with a metadata token
    /// operand, up to the first that cannot be decoded, which is noted in <paramref name="passedOver"/>. Each is
    /// decoded under a catch of its own, and handed on outside it.</summary>
    public static IEnumerable<(ILOpCode OpCode, EntityHandle Token)> ListTokens(MetadataFile module, MethodBodyBlock body, PassedOver passedOver)
    {
        var blob = ReadIL(module, body, passedOver);
        if (blob is null)
        {
            yield break;
        }

        var reader = blob.Value;
        while (reader.RemainingBytes > 0)
        {
            var read = TryReadToken(ref reader, out var opCode, out var token);
            if (read is not null)
            {
                passedOver.Note(module, read);
                yield break;
            }

            if (!token.IsNil)
            {
                yield return (opCode, token);
            }
        }
    }

    /// <summary>Whether <paramref name="match"/> accepts a token operand of <paramref name="body"/>, of
    /// <paramref name="module"/>; a token it cannot resolve is not a match, and is noted in
    /// <paramref name="passedOver"/>.</summary>
    public static bool HasToken(MetadataFile module, MethodBodyBlock body, PassedOver passedOver, Func<ILOpCode, EntityHandle, bool> match)
    {
        return ListTokens(module, body, passedOver).Any(t => Matches(module, passedOver, match, t.OpCode, t.Token));
    }

    private static bool Matches(MetadataFile module, PassedOver passedOver, Func<ILOpCode, EntityHandle, bool> match, ILOpCode opCode, EntityHandle token)
    {
        try
        {
            return match(opCode, token);
        }
        catch (BadImageFormatException e)
        {
            passedOver.Note(module, e);
            return false;
        }
    }

    private static BlobReader? ReadIL(MetadataFile module, MethodBodyBlock body, PassedOver passedOver)
    {
        try
        {
            return body.GetILReader();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(module, e);
            return null;
        }
    }

    // Null when the instruction was read, else why it couldn't be.
    private static Exception? TryReadToken(ref BlobReader blob, out ILOpCode opCode, out EntityHandle token)
    {
        token = default;
        try
        {
            opCode = blob.DecodeOpCode();
            var operandType = opCode.GetOperandType();
            if (operandType is OperandType.Field or OperandType.Method or OperandType.Sig or OperandType.Tok or OperandType.Type)
            {
                token = MetadataTokenHelpers.EntityHandleOrNil(blob.ReadInt32());
            }
            else
            {
                blob.SkipOperand(opCode);
            }

            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            opCode = default;
            return e;
        }
    }
}
