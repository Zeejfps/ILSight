// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>Shows the members whose code uses a method, each compiler-generated method standing for
/// the method it came from. A virtual call to the member a non-virtual method overrides counts.</summary>
internal static class MethodUsedByAnalyzer
{
    public static AnalysisRun? Prepare(ISymbol? symbol)
    {
        if (symbol is not IMethod method || (!method.IsVirtual && method.ParentModule is null))
        {
            return null;
        }

        return context => Analyze(method, context);
    }

    internal static IEnumerable<ISymbol> Analyze(IMethod analyzed, AnalyzerContext context)
    {
        if (analyzed.ParentModule?.MetadataFile is null || analyzed.DeclaringTypeDefinition?.MetadataToken.IsNil != false)
        {
            yield break;
        }

        IMethod? baseMethod = null;
        if (!analyzed.IsVirtual)
        {
            var baseMember = InheritanceHelper.GetBaseMember(analyzed);
            if (baseMember is IMethod found)
            {
                baseMethod = found;
            }
        }

        var parent = ParentOf(analyzed);
        if (parent.MetadataToken != analyzed.MetadataToken)
        {
            yield return parent;
        }

        var users = AnalyzerScope.Create(context.AssemblyList, analyzed).FindMembersWithCode(
            method => method.ParentModule is MetadataModule module
                && MethodBodies.HasToken(MethodBodies.Find(method), (opCode, token) => References(module, opCode, token)),
            context.CancellationToken);
        foreach (var user in users)
        {
            yield return user is IMethod method ? ParentOf(method) : user;
        }

        IMethod ParentOf(IMethod method)
        {
            return DecompilerFacts.GetMetadataModule(method).GetDefinition(
                context.EnsureMapping(DecompilerFacts.GetDeclaringTypeDefinition(method)).GetParentMethod(DecompilerFacts.GetMethodHandle(method)));
        }

        bool References(MetadataModule module, ILOpCode opCode, EntityHandle token)
        {
            if (opCode is not (ILOpCode.Call or ILOpCode.Callvirt or ILOpCode.Ldtoken or ILOpCode.Ldftn or ILOpCode.Ldvirtftn or ILOpCode.Newobj)
                && !(analyzed.IsVirtual && opCode == ILOpCode.Jmp))
            {
                return false;
            }

            if (!IsPossibleReferenceTo(token, module.MetadataFile, analyzed)
                && (baseMethod is null || !IsPossibleReferenceTo(token, module.MetadataFile, baseMethod)))
            {
                return false;
            }

            var member = (module.ResolveEntity(token) as IMember)?.MemberDefinition;
            return member is not null && (opCode == ILOpCode.Callvirt && baseMethod is not null && Members.IsSame(member, baseMethod) || Members.IsSame(member, analyzed));
        }
    }

    private static bool IsPossibleReferenceTo(EntityHandle member, MetadataFile module, IMethod analyzedMethod)
    {
        if (member.IsNil)
        {
            return false;
        }

        var metadata = module.Metadata;
        switch (member.Kind)
        {
            case HandleKind.MethodDefinition:
                return member == analyzedMethod.MetadataToken && module == analyzedMethod.ParentModule?.MetadataFile;
            case HandleKind.MemberReference:
                var mr = metadata.GetMemberReference((MemberReferenceHandle)member);
                return mr.GetKind() == MemberReferenceKind.Method && metadata.StringComparer.Equals(mr.Name, analyzedMethod.Name);
            case HandleKind.MethodSpecification:
                var ms = metadata.GetMethodSpecification((MethodSpecificationHandle)member);
                return IsPossibleReferenceTo(ms.Method, module, analyzedMethod);
            default:
                return false;
        }
    }
}
