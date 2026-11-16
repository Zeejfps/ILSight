// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Method bodies as the analyzers read them: the body of a method, and the metadata tokens
/// its instructions name. What the library can't read of them is noted in a <see cref="PassedOver"/>, so the analysis
/// says the module partly read.</summary>
internal static class MethodBodies
{
    /// <summary>The body of method <paramref name="handle"/>; null when it has none, or it cannot be read, which is
    /// noted in <paramref name="passedOver"/>.</summary>
    public static MethodBodyBlock? Read(MetadataFile module, MethodDefinitionHandle handle, PassedOver passedOver)
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
        catch (BadImageFormatException e)
        {
            passedOver.Note(module, e);
            return null;
        }
    }

    /// <summary>Whether <paramref name="match"/> accepts a token operand of the body of <paramref name="method"/>;
    /// false when it has none. What can't be read is noted in <paramref name="passedOver"/>.</summary>
    public static bool HasToken(IMethod method, PassedOver passedOver, Func<ILOpCode, EntityHandle, bool> match)
    {
        var module = method.ParentModule?.MetadataFile;
        if (!method.HasBody || method.MetadataToken.IsNil || module is null)
        {
            return false;
        }

        return HasToken(module, DecompilerFacts.GetMethodHandle(method), passedOver, match);
    }

    /// <summary>Whether <paramref name="match"/> accepts a token operand of the body of method
    /// <paramref name="handle"/>; false when it has none. What can't be read is noted in
    /// <paramref name="passedOver"/>.</summary>
    public static bool HasToken(MetadataFile module, MethodDefinitionHandle handle, PassedOver passedOver, Func<ILOpCode, EntityHandle, bool> match)
    {
        var body = Read(module, handle, passedOver);
        return body is not null && HasToken(module, body, passedOver, match);
    }

    /// <summary>The instructions of <paramref name="body"/>, of <paramref name="module"/>, with a metadata token
    /// operand, up to the first that cannot be decoded, which is noted in <paramref name="passedOver"/>.</summary>
    public static IEnumerable<(ILOpCode OpCode, EntityHandle Token)> ListTokens(MetadataFile module, MethodBodyBlock body, PassedOver passedOver)
    {
        var blob = body.GetILReader();
        while (blob.RemainingBytes > 0)
        {
            var read = TryReadToken(ref blob, out var opCode, out var token);
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

    // Null when the instruction was read, else why it couldn't be.
    private static BadImageFormatException? TryReadToken(ref BlobReader blob, out ILOpCode opCode, out EntityHandle token)
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
        catch (BadImageFormatException e)
        {
            opCode = default;
            return e;
        }
    }
}
