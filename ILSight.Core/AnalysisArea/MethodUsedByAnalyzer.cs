// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Shows the members whose code uses a method, each compiler-generated method standing for
/// the method it came from. A virtual call to the member a non-virtual method overrides counts.</summary>
/// <remarks>Each step reads under a catch of its own, noting a part the library fails reading as passed over: the
/// analyzed method's facts and its base's, where a method's code is, its body and instructions, what a call's token
/// names, the member it resolves to, and the method a compiler-generated one came from. Which opcodes use a method,
/// whether a token may name the analyzed one, and whether the member resolved is it, are decided outside.</remarks>
internal static class MethodUsedByAnalyzer
{
    private static readonly ILOpCode[] UsingOpCodes = [ILOpCode.Call, ILOpCode.Callvirt, ILOpCode.Ldtoken, ILOpCode.Ldftn, ILOpCode.Ldvirtftn, ILOpCode.Newobj];

    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not IMethod method)
        {
            return null;
        }

        return context => Analyze(method, context);
    }

    /// <summary>A method that is virtual or in a module.</summary>
    public static bool Applies(OfferFacts facts)
    {
        return facts.Sort == AnalyzedSort.Method && (facts.IsVirtual || facts.HasParentModule);
    }

    internal static IEnumerable<ISymbol> Analyze(IMethod analyzed, AnalyzerContext context)
    {
        var facts = ReadAnalyzed(analyzed, context);
        if (facts is null)
        {
            yield break;
        }

        var parent = ReadParentOf(analyzed, context.AnalyzedFile, context);
        if (parent is not null && parent.Token != facts.Method.Key.Token)
        {
            yield return parent.Method;
        }

        var scope = AnalyzerScope.Create(context, analyzed);
        if (scope is null)
        {
            yield break;
        }

        var users = scope.FindMembersWithCode((file, method) => Calls(file, method, facts, context.PassedOver), context.CancellationToken);
        foreach (var user in users)
        {
            if (user is not IMethod method)
            {
                yield return user;
                continue;
            }

            var userParent = ReadParentOf(method, context.AnalyzedFile, context);
            if (userParent is not null)
            {
                yield return userParent.Method;
            }
        }
    }

    // The analyzed method's facts, and when it isn't virtual those of the method it overrides; null for a method of no
    // file or no type with a token, or when the library fails reading them, noted.
    private static AnalyzedFacts? ReadAnalyzed(IMethod analyzed, AnalyzerContext context)
    {
        try
        {
            if (analyzed.ParentModule?.MetadataFile is null || analyzed.DeclaringTypeDefinition?.MetadataToken.IsNil != false)
            {
                return null;
            }

            var baseMethod = analyzed.IsVirtual ? null : InheritanceHelper.GetBaseMember(analyzed) as IMethod;
            return new AnalyzedFacts
            {
                Method = ReadFacts(analyzed), IsVirtual = analyzed.IsVirtual, Base = baseMethod is null ? null : ReadFacts(baseMethod),
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }
    }

    private static MethodFacts ReadFacts(IMethod method)
    {
        return new MethodFacts { Key = DefinitionKey.Of(method), Name = method.Name };
    }

    private sealed class AnalyzedFacts
    {
        public required MethodFacts Method { get; init; }

        public required bool IsVirtual { get; init; }

        // Null for a virtual method, or one overriding none.
        public required MethodFacts? Base { get; init; }
    }

    private sealed class MethodFacts
    {
        public required DefinitionKey Key { get; init; }

        public required string Name { get; init; }
    }

    // The method whose code the method is part of, with its token; null when the library fails reading it, noted.
    private static ParentMethod? ReadParentOf(IMethod method, MetadataFile file, AnalyzerContext context)
    {
        var code = MethodBodies.ReadCode(method, file, context.PassedOver);
        if (code is null)
        {
            return null;
        }

        var parent = MethodBodies.ReadParent(context, code.File, code.Declaring, code.Handle);
        if (parent is null)
        {
            return null;
        }

        return ReadDefinition(code, parent.Value, context.PassedOver);
    }

    private static ParentMethod? ReadDefinition(MethodCode code, MethodDefinitionHandle handle, PassedOver passedOver)
    {
        try
        {
            var method = code.Module.GetDefinition(handle);
            return new ParentMethod { Method = method, Token = method.MetadataToken };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(code.File, e);
            return null;
        }
    }

    private sealed class ParentMethod
    {
        public required IMethod Method { get; init; }

        public required EntityHandle Token { get; init; }
    }

    private static bool Calls(MetadataFile file, IMethod method, AnalyzedFacts analyzed, PassedOver passedOver)
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
            if (References(code, opCode, token, analyzed, passedOver))
            {
                return true;
            }
        }
        return false;
    }

    private static bool References(MethodCode code, ILOpCode opCode, EntityHandle token, AnalyzedFacts analyzed, PassedOver passedOver)
    {
        if (!UsingOpCodes.Contains(opCode) && !(analyzed.IsVirtual && opCode == ILOpCode.Jmp))
        {
            return false;
        }

        var target = ReadCallTarget(code, token, analyzed, passedOver);
        if (target is null)
        {
            return false;
        }

        var possible = IsPossibleReferenceTo(target.Value, code.File, analyzed.Method, target.Value.NamedAsAnalyzed)
            || analyzed.Base is not null && IsPossibleReferenceTo(target.Value, code.File, analyzed.Base, target.Value.NamedAsBase);
        if (!possible)
        {
            return false;
        }

        var member = ReadMemberDefinition(code, token, passedOver);
        if (member is null)
        {
            return false;
        }

        return opCode == ILOpCode.Callvirt && analyzed.Base is not null && member.Value.Is(analyzed.Base.Key) || member.Value.Is(analyzed.Method.Key);
    }

    // ILSpy's test of whether a token may name the method, before resolving it: a definition by its token in the
    // method's own file, a reference to a method by the method's name.
    private static bool IsPossibleReferenceTo(CallTarget target, MetadataFile module, MethodFacts method, bool namedAsMethod)
    {
        if (!target.Definition.IsNil)
        {
            return target.Definition == method.Key.Token && module == method.Key.File;
        }

        return namedAsMethod;
    }

    // What the token names, through a method specification to its method; null when the library fails reading it,
    // noted.
    private static CallTarget? ReadCallTarget(MethodCode code, EntityHandle token, AnalyzedFacts analyzed, PassedOver passedOver)
    {
        try
        {
            return ReadTarget(code.File.Metadata, token, analyzed.Method.Name, analyzed.Base?.Name);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(code.File, e);
            return null;
        }
    }

    private static CallTarget ReadTarget(MetadataReader metadata, EntityHandle member, string name, string? baseName)
    {
        if (member.IsNil)
        {
            return default;
        }

        switch (member.Kind)
        {
            case HandleKind.MethodDefinition:
                return new CallTarget { Definition = member };
            case HandleKind.MemberReference:
                var mr = metadata.GetMemberReference((MemberReferenceHandle)member);
                var isMethod = mr.GetKind() == MemberReferenceKind.Method;
                return new CallTarget
                {
                    NamedAsAnalyzed = isMethod && metadata.StringComparer.Equals(mr.Name, name),
                    NamedAsBase = isMethod && baseName is not null && metadata.StringComparer.Equals(mr.Name, baseName),
                };
            case HandleKind.MethodSpecification:
                var ms = metadata.GetMethodSpecification((MethodSpecificationHandle)member);
                return ReadTarget(metadata, ms.Method, name, baseName);
            default:
                return default;
        }
    }

    // What a call's token names, read: the method definition, or whether the method reference is named as the analyzed
    // method or as its base; neither for anything else.
    private readonly record struct CallTarget
    {
        public EntityHandle Definition { get; init; }

        public bool NamedAsAnalyzed { get; init; }

        public bool NamedAsBase { get; init; }
    }

    // The definition of the member the token resolves to; null when it resolves to none, or the library fails
    // resolving it, noted.
    private static DefinitionKey? ReadMemberDefinition(MethodCode code, EntityHandle token, PassedOver passedOver)
    {
        try
        {
            var member = (code.Module.ResolveEntity(token) as IMember)?.MemberDefinition;
            return member is null ? null : DefinitionKey.Of(member);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(code.File, e);
            return null;
        }
    }
}
