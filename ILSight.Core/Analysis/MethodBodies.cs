// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Method bodies as the analyzers read them: the body of a method, and the metadata tokens
/// its instructions name.</summary>
internal static class MethodBodies
{
    /// <summary>The body of method <paramref name="handle"/>; null when it has none or it cannot be read.</summary>
    public static MethodBodyBlock? Read(MetadataFile module, MethodDefinitionHandle handle)
    {
        var definition = module.Metadata.GetMethodDefinition(handle);
        if (!definition.HasBody())
        {
            return null;
        }

        try
        {
            return module.GetMethodBody(definition.RelativeVirtualAddress);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>The body of <paramref name="method"/>; null when it has none or it cannot be read.</summary>
    public static MethodBodyBlock? Find(IMethod method)
    {
        var module = method.ParentModule?.MetadataFile;
        if (!method.HasBody || method.MetadataToken.IsNil || module is null)
        {
            return null;
        }

        return Read(module, DecompilerFacts.GetMethodHandle(method));
    }

    /// <summary>The instructions of <paramref name="body"/> with a metadata token operand, up to the
    /// first that cannot be decoded.</summary>
    public static IEnumerable<(ILOpCode OpCode, EntityHandle Token)> ListTokens(MethodBodyBlock body)
    {
        var blob = body.GetILReader();
        while (blob.RemainingBytes > 0)
        {
            if (!TryReadToken(ref blob, out var opCode, out var token))
            {
                yield break;
            }

            if (!token.IsNil)
            {
                yield return (opCode, token);
            }
        }
    }

    /// <summary>Whether <paramref name="match"/> accepts a token operand of <paramref name="body"/>;
    /// a token it cannot resolve is not a match.</summary>
    public static bool HasToken(MethodBodyBlock? body, Func<ILOpCode, EntityHandle, bool> match)
    {
        return body is not null && ListTokens(body).Any(t => Matches(match, t.OpCode, t.Token));
    }

    private static bool Matches(Func<ILOpCode, EntityHandle, bool> match, ILOpCode opCode, EntityHandle token)
    {
        try
        {
            return match(opCode, token);
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static bool TryReadToken(ref BlobReader blob, out ILOpCode opCode, out EntityHandle token)
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

            return true;
        }
        catch (BadImageFormatException)
        {
            opCode = default;
            return false;
        }
    }
}
