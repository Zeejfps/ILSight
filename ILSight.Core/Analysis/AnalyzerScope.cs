// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;

namespace ILSight.Core.Analysis;

/// <summary>
/// Where the symbols related to an analyzed entity can be: only inside its type when it is
/// private, its assembly and the friends it shows internals to when it is internal, and otherwise
/// every open assembly that references its type.
/// </summary>
internal sealed class AnalyzerScope
{
    private const GetMemberOptions MemberDefinitions = GetMemberOptions.IgnoreInheritedMembers | GetMemberOptions.ReturnMemberDefinitions;

    public required AssemblyList List { get; init; }

    public required IReadOnlyList<LoadedModule> Assemblies { get; init; }

    public required Accessibility EffectiveAccessibility { get; init; }

    /// <summary>Whether the analyzed symbol is reachable only from inside <see cref="TypeScope"/>.</summary>
    public required bool IsLocal { get; init; }

    public required ITypeDefinition TypeScope { get; init; }

    /// <summary>Where the symbols related to <paramref name="entity"/>, of the assemblies of <paramref name="list"/>, can be.</summary>
    public static AnalyzerScope Create(AssemblyList list, IEntity entity)
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

    public IEnumerable<MetadataFile> GetModules(CancellationToken ct)
    {
        if (IsLocal)
        {
            return [DecompilerFacts.GetMetadataFile(TypeScope)];
        }

        if (EffectiveAccessibility.LessThanOrEqual(Accessibility.Internal))
        {
            return GetModuleAndAnyFriends(TypeScope, ct);
        }

        return GetReferencingModules(DecompilerFacts.GetMetadataFile(TypeScope), ct);
    }

    public DecompilerTypeSystem GetTypeSystem(MetadataFile module)
    {
        return AssemblyListing.EnsureTypeSystemOf(List, module);
    }

    public IEnumerable<ITypeDefinition> GetTypes(CancellationToken ct)
    {
        if (IsLocal)
        {
            foreach (var type in TreeTraversal.PreOrder(TypeScope, static t => t.NestedTypes))
            {
                yield return type;
            }
        }
        else
        {
            foreach (var module in GetModules(ct))
            {
                foreach (var type in GetTypeSystem(module).MainModule.TypeDefinitions)
                {
                    yield return type;
                }
            }
        }
    }

    /// <summary>The members in scope with code <paramref name="matches"/> accepts, type by type:
    /// each such method, then each property and event with such an accessor.</summary>
    public IEnumerable<IEntity> FindMembersWithCode(Func<IMethod, bool> matches, CancellationToken ct)
    {
        foreach (var type in GetTypes(ct))
        {
            if (type.ParentModule?.MetadataFile is null)
            {
                continue;
            }

            foreach (var method in type.GetMembers(static m => m is IMethod, MemberDefinitions).OfType<IMethod>())
            {
                if (matches(method))
                {
                    yield return method;
                }
            }
            foreach (var member in type.Properties.Concat<IMember>(type.Events))
            {
                var accessors = Members.Accessors(member);
                if (accessors.Any(matches))
                {
                    yield return member;
                }
            }
        }
    }

    private static void DetermineEffectiveAccessibility(IEntity input, out ITypeDefinition typeScope, out Accessibility accessibility)
    {
        ITypeDefinition start;
        if (input is ITypeDefinition definition)
        {
            accessibility = Accessibility.Public;
            start = definition;
        }
        else
        {
            accessibility = input.Accessibility;
            start = DecompilerFacts.GetDeclaringTypeDefinition(input);
        }
        var previous = start;
        var current = start;
        while (current is not null && !accessibility.LessThanOrEqual(Accessibility.Private))
        {
            accessibility = accessibility.Intersect(current.Accessibility);
            previous = current;
            current = previous.DeclaringTypeDefinition;
        }
        typeScope = current ?? previous;
    }

    private IEnumerable<MetadataFile> GetReferencingModules(MetadataFile self, CancellationToken ct)
    {
        yield return self;
        var scopeName = TypeScope.Name;
        if (TypeScope.TypeParameterCount > 0)
        {
            scopeName += "`" + TypeScope.TypeParameterCount;
        }

        var toWalk = new Stack<MetadataFile>();
        var checkedFiles = new HashSet<MetadataFile> { self };
        toWalk.Push(self);
        do
        {
            var current = toWalk.Pop();
            foreach (var assembly in Assemblies)
            {
                ct.ThrowIfCancellationRequested();
                var module = assembly.File;
                if (!module.IsAssembly || checkedFiles.Contains(module))
                {
                    continue;
                }

                var found = false;
                foreach (var reference in module.AssemblyReferences)
                {
                    var known = AssemblyListing.FindKnown(List, reference, exactOrNewer: true);
                    if (known?.File == current)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found || !checkedFiles.Add(module))
                {
                    continue;
                }

                if (ReferencesScopeType(module.Metadata, scopeName, TypeScope.Namespace))
                {
                    yield return module;
                }

                if (ForwardsScopeType(module.Metadata, scopeName, TypeScope.Namespace))
                {
                    toWalk.Push(module);
                }
            }
        } while (toWalk.Count > 0);
    }

    private IEnumerable<MetadataFile> GetModuleAndAnyFriends(ITypeDefinition typeScope, CancellationToken ct)
    {
        var self = DecompilerFacts.GetMetadataFile(typeScope);
        yield return self;
        var provider = MetadataExtensions.MinimalAttributeTypeProvider;
        var friends = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handle in self.Metadata.CustomAttributes)
        {
            var attribute = self.Metadata.GetCustomAttribute(handle);
            if (!IsInternalsVisibleTo(self.Metadata, attribute))
            {
                continue;
            }

            var value = attribute.DecodeValue(provider);
            if (value.FixedArguments[0].Value is string name)
            {
                friends.Add(name.Split(',')[0]);
            }
        }
        if (friends.Count == 0)
        {
            yield break;
        }

        foreach (var assembly in Assemblies)
        {
            ct.ThrowIfCancellationRequested();
            if (!friends.Contains(assembly.Name) || assembly.File.IsMetadataOnly)
            {
                continue;
            }

            if (ReferencesScopeType(assembly.File.Metadata, typeScope.Name, typeScope.Namespace))
            {
                yield return assembly.File;
            }
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

    private static bool ReferencesScopeType(MetadataReader metadata, string typeScopeName, string typeScopeNamespace)
    {
        foreach (var handle in metadata.TypeReferences)
        {
            var typeRef = metadata.GetTypeReference(handle);
            if (metadata.StringComparer.Equals(typeRef.Name, typeScopeName) && metadata.StringComparer.Equals(typeRef.Namespace, typeScopeNamespace))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ForwardsScopeType(MetadataReader metadata, string typeScopeName, string typeScopeNamespace)
    {
        foreach (var handle in metadata.ExportedTypes)
        {
            var exported = metadata.GetExportedType(handle);
            if (exported.IsForwarder && metadata.StringComparer.Equals(exported.Name, typeScopeName)
                && metadata.StringComparer.Equals(exported.Namespace, typeScopeNamespace))
            {
                return true;
            }
        }
        return false;
    }
}
