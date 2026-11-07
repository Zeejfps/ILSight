using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Util;
// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

internal static class AnalyzerHelpers
{
    /// <summary>Whether both name the same definition: the same token in the same file.</summary>
    public static bool IsSame(IEntity entity, IEntity other)
    {
        return entity.MetadataToken == other.MetadataToken && entity.ParentModule?.MetadataFile == other.ParentModule?.MetadataFile;
    }

    /// <summary>The body of method <paramref name="handle"/>; null when it has none or it cannot be read.</summary>
    public static MethodBodyBlock? ReadBody(MetadataFile module, MethodDefinitionHandle handle)
    {
        var definition = module.Metadata.GetMethodDefinition(handle);
        if (!definition.HasBody())
            return null;
        try
        {
            return module.GetMethodBody(definition.RelativeVirtualAddress);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>The instructions of <paramref name="body"/> with a metadata token operand, up to the
    /// first that cannot be decoded.</summary>
    public static IEnumerable<(ILOpCode OpCode, EntityHandle Token)> ListTokens(MethodBodyBlock body)
    {
        var blob = body.GetILReader();
        while (blob.RemainingBytes > 0)
        {
            if (!TryReadToken(ref blob, out var opCode, out var token))
                yield break;
            if (!token.IsNil)
                yield return (opCode, token);
        }
    }

    /// <summary>Whether <paramref name="match"/> accepts a token operand of <paramref name="body"/>;
    /// a token it cannot resolve is not a match.</summary>
    public static bool HasToken(MethodBodyBlock? body, Func<ILOpCode, EntityHandle, bool> match)
    {
        return body != null && ListTokens(body).Any(t => Matches(match, t.OpCode, t.Token));
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
            if (opCode.GetOperandType() is OperandType.Field or OperandType.Method or OperandType.Sig or OperandType.Tok or OperandType.Type)
                token = MetadataTokenHelpers.EntityHandleOrNil(blob.ReadInt32());
            else
                blob.SkipOperand(opCode);
            return true;
        }
        catch (BadImageFormatException)
        {
            opCode = default;
            return false;
        }
    }

    public static bool IsPossibleReferenceTo(EntityHandle member, MetadataFile module, IMethod analyzedMethod)
    {
        if (member.IsNil)
            return false;
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

    public static bool IsCustomAttributeOfType(EntityHandle customAttributeCtor, MetadataReader metadata, FindTypeDecoder decoder) =>
        decoder.GetTypeFromEntity(metadata, customAttributeCtor.GetDeclaringType(metadata));

    /// <summary>What carries each attribute of <paramref name="ts"/>'s module that
    /// <paramref name="matches"/> accepts, in attribute order, then the method, or the property or
    /// event it is an accessor of, for each parameter that carries one.</summary>
    public static IEnumerable<ISymbol> AttributeOwners(DecompilerTypeSystem ts, Func<CustomAttribute, bool> matches)
    {
        var metadata = ts.MainModule.MetadataFile.Metadata;
        var parameters = new HashSet<ParameterHandle>();
        foreach (var h in metadata.CustomAttributes)
        {
            var attribute = metadata.GetCustomAttribute(h);
            if (!matches(attribute))
                continue;
            if (attribute.Parent.Kind == HandleKind.Parameter)
                parameters.Add((ParameterHandle)attribute.Parent);
            else
            {
                var parent = GetParentEntity(ts, attribute);
                if (parent != null)
                    yield return parent;
            }
        }
        if (parameters.Count == 0)
            yield break;
        foreach (var h in metadata.MethodDefinitions)
        {
            if (!metadata.GetMethodDefinition(h).GetParameters().Any(parameters.Contains))
                continue;
            var method = ts.MainModule.ResolveMethod(h, default);
            if (method != null)
                yield return method.IsAccessor ? method.AccessorOwner : method;
        }
    }

    private static ISymbol? GetParentEntity(DecompilerTypeSystem ts, CustomAttribute customAttribute)
    {
        var metadata = ts.MainModule.MetadataFile.Metadata;
        switch (customAttribute.Parent.Kind)
        {
            case HandleKind.MethodDefinition:
                var parent = (IMethod)ts.MainModule.ResolveEntity(customAttribute.Parent);
                return parent?.AccessorOwner ?? parent;
            case HandleKind.FieldDefinition:
            case HandleKind.PropertyDefinition:
            case HandleKind.EventDefinition:
            case HandleKind.TypeDefinition:
                return ts.MainModule.ResolveEntity(customAttribute.Parent);
            case HandleKind.AssemblyDefinition:
            case HandleKind.ModuleDefinition:
                return ts.MainModule;
            case HandleKind.GenericParameterConstraint:
                var gpc = metadata.GetGenericParameterConstraint((GenericParameterConstraintHandle)customAttribute.Parent);
                var gp = metadata.GetGenericParameter(gpc.Parameter);
                return ts.MainModule.ResolveEntity(gp.Parent);
            case HandleKind.GenericParameter:
                gp = metadata.GetGenericParameter((GenericParameterHandle)customAttribute.Parent);
                return ts.MainModule.ResolveEntity(gp.Parent);
            default:
                return null;
        }
    }

    internal static AnalyzerScope GetScope(AnalyzerContext context, IEntity entity)
    {
        return CreateScope(context.AssemblyList, entity);
    }

    internal static MethodBodyBlock? GetMethodBody(AnalyzerContext context, IMethod method)
    {
        var module = method.ParentModule?.MetadataFile;
        if (!method.HasBody || method.MetadataToken.IsNil || module == null)
            return null;
        return AnalyzerHelpers.ReadBody(module, (MethodDefinitionHandle)method.MetadataToken);
    }

    /// <summary>How C# maps the compiler-generated code around <paramref name="entity"/> to the
    /// methods it came from; the last one asked for is kept.</summary>
    internal static CodeMappingInfo EnsureMapping(AnalyzerContext context, IEntity entity)
    {
        var file = entity.ParentModule!.MetadataFile!;
        var mapping = context.Mapping;
        if (mapping == null || mapping.Value.File != file || mapping.Value.Handle != entity.MetadataToken)
        {
            mapping = (file, entity.MetadataToken, CSharpDecompiler.GetCodeMappingInfo(file, entity.MetadataToken));
            context.Mapping = mapping;
        }
        return mapping.Value.Mapping;
    }

    private const GetMemberOptions MemberDefinitions = GetMemberOptions.IgnoreInheritedMembers | GetMemberOptions.ReturnMemberDefinitions;

    /// <summary>Where the symbols related to <paramref name="entity"/>, of the assemblies of <paramref name="list"/>, can be.</summary>
    internal static AnalyzerScope CreateScope(AssemblyList list, IEntity entity)
    {
        DetermineEffectiveAccessibility(entity, out var typeScope, out var accessibility);
        return new AnalyzerScope
        {
            List = list,
            Assemblies = list.Assemblies,
            EffectiveAccessibility = accessibility,
            TypeScope = typeScope,
            IsLocal = accessibility.LessThanOrEqual(Accessibility.Private),
        };
    }

    internal static IEnumerable<MetadataFile> GetModulesInScope(AnalyzerScope scope, CancellationToken ct)
    {
        if (scope.IsLocal)
            return [scope.TypeScope.ParentModule!.MetadataFile!];
        if (scope.EffectiveAccessibility.LessThanOrEqual(Accessibility.Internal))
            return GetModuleAndAnyFriends(scope, scope.TypeScope, ct);
        return GetReferencingModules(scope, scope.TypeScope.ParentModule!.MetadataFile!, ct);
    }

    internal static DecompilerTypeSystem GetTypeSystem(AnalyzerScope scope, MetadataFile module)
    {
        return scope.List.TypeSystemOf(module);
    }

    internal static IEnumerable<ITypeDefinition> GetTypesInScope(AnalyzerScope scope, CancellationToken ct)
    {
        if (scope.IsLocal)
        {
            foreach (var type in TreeTraversal.PreOrder(scope.TypeScope, static t => t.NestedTypes))
                yield return type;
        }
        else
        {
            foreach (var module in GetModulesInScope(scope, ct))
            {
                foreach (var type in GetTypeSystem(scope, module).MainModule.TypeDefinitions)
                    yield return type;
            }
        }
    }

    /// <summary>The members in scope with code <paramref name="matches"/> accepts, type by type:
    /// each such method, then each property and event with such an accessor.</summary>
    internal static IEnumerable<IEntity> FindMembersWithCode(AnalyzerScope scope, Func<IMethod, bool> matches, CancellationToken ct)
    {
        foreach (var type in GetTypesInScope(scope, ct))
        {
            if (type.ParentModule?.MetadataFile == null)
                continue;
            foreach (var method in type.GetMembers(static m => m is IMethod, MemberDefinitions).OfType<IMethod>())
            {
                if (matches(method))
                    yield return method;
            }
            foreach (var member in type.Properties.Concat<IMember>(type.Events))
            {
                if (Members.Accessors(member).Any(matches))
                    yield return member;
            }
        }
    }

    private static void DetermineEffectiveAccessibility(IEntity input, out ITypeDefinition typeScope, out Accessibility accessibility)
    {
        if (input is ITypeDefinition definition)
        {
            accessibility = Accessibility.Public;
            typeScope = definition;
        }
        else
        {
            accessibility = input.Accessibility;
            typeScope = input.DeclaringTypeDefinition!;
        }
        var previous = typeScope;
        while (typeScope != null && !accessibility.LessThanOrEqual(Accessibility.Private))
        {
            accessibility = accessibility.Intersect(typeScope.Accessibility);
            previous = typeScope;
            typeScope = previous.DeclaringTypeDefinition!;
        }
        typeScope ??= previous;
    }

    private static IEnumerable<MetadataFile> GetReferencingModules(AnalyzerScope scope, MetadataFile self, CancellationToken ct)
    {
        yield return self;
        var scopeName = scope.TypeScope.Name;
        if (scope.TypeScope.TypeParameterCount > 0)
            scopeName += "`" + scope.TypeScope.TypeParameterCount;
        var toWalk = new Stack<MetadataFile>();
        var checkedFiles = new HashSet<MetadataFile> { self };
        toWalk.Push(self);
        do
        {
            var current = toWalk.Pop();
            foreach (var assembly in scope.Assemblies)
            {
                ct.ThrowIfCancellationRequested();
                var module = assembly.File;
                if (!module.IsAssembly || checkedFiles.Contains(module)) continue;
                var found = false;
                foreach (var reference in module.AssemblyReferences)
                {
                    if (scope.List.FindKnown(reference, exactOrNewer: true)?.File == current)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found || !checkedFiles.Add(module)) continue;
                if (ModuleReferencesScopeType(module.Metadata, scopeName, scope.TypeScope.Namespace))
                    yield return module;
                if (ModuleForwardsScopeType(module.Metadata, scopeName, scope.TypeScope.Namespace))
                    toWalk.Push(module);
            }
        } while (toWalk.Count > 0);
    }

    private static IEnumerable<MetadataFile> GetModuleAndAnyFriends(AnalyzerScope scope, ITypeDefinition typeScope, CancellationToken ct)
    {
        var self = typeScope.ParentModule!.MetadataFile!;
        yield return self;
        var provider = MetadataExtensions.MinimalAttributeTypeProvider;
        var friends = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handle in self.Metadata.CustomAttributes)
        {
            var attribute = self.Metadata.GetCustomAttribute(handle);
            if (!IsInternalsVisibleTo(self.Metadata, attribute)) continue;
            if (attribute.DecodeValue(provider).FixedArguments[0].Value is string name)
                friends.Add(name.Split(',')[0]);
        }
        if (friends.Count == 0) yield break;
        foreach (var assembly in scope.Assemblies)
        {
            ct.ThrowIfCancellationRequested();
            if (!friends.Contains(assembly.Name) || assembly.File.IsMetadataOnly) continue;
            if (ModuleReferencesScopeType(assembly.File.Metadata, typeScope.Name, typeScope.Namespace))
                yield return assembly.File;
        }
    }

    private static bool IsInternalsVisibleTo(MetadataReader metadata, CustomAttribute attribute)
    {
        var type = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition => metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            _ => default(EntityHandle),
        };
        var (ns, name) = type.Kind switch
        {
            HandleKind.TypeReference when metadata.GetTypeReference((TypeReferenceHandle)type) is var r => (r.Namespace, r.Name),
            HandleKind.TypeDefinition when metadata.GetTypeDefinition((TypeDefinitionHandle)type) is var d => (d.Namespace, d.Name),
            _ => (default(StringHandle), default(StringHandle)),
        };
        return !name.IsNil && metadata.StringComparer.Equals(ns, "System.Runtime.CompilerServices")
            && metadata.StringComparer.Equals(name, "InternalsVisibleToAttribute");
    }

    private static bool ModuleReferencesScopeType(MetadataReader metadata, string typeScopeName, string typeScopeNamespace)
    {
        foreach (var handle in metadata.TypeReferences)
        {
            var typeRef = metadata.GetTypeReference(handle);
            if (metadata.StringComparer.Equals(typeRef.Name, typeScopeName) && metadata.StringComparer.Equals(typeRef.Namespace, typeScopeNamespace))
                return true;
        }
        return false;
    }

    private static bool ModuleForwardsScopeType(MetadataReader metadata, string typeScopeName, string typeScopeNamespace)
    {
        foreach (var handle in metadata.ExportedTypes)
        {
            var exported = metadata.GetExportedType(handle);
            if (exported.IsForwarder && metadata.StringComparer.Equals(exported.Name, typeScopeName)
                && metadata.StringComparer.Equals(exported.Namespace, typeScopeNamespace))
                return true;
        }
        return false;
    }
}
