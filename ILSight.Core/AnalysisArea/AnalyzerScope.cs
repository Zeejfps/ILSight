// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Collections.Concurrent;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;
using AssemblyReference = ICSharpCode.Decompiler.Metadata.AssemblyReference;

namespace ILSight.Core.AnalysisArea;

/// <summary>
/// Where the symbols related to an analyzed entity can be: only inside its type when it is
/// private, its assembly and the friends it shows internals to when it is internal, and otherwise
/// every open assembly that references its type.
/// </summary>
/// <remarks>The scan reads the library's objects a step at a time, each read under a catch of its own, and decides
/// outside it which module or type to read next. For the analyzers a part the library fails reading (a module's
/// references, a type's nested types or members) is noted in <see cref="PassedOver"/> and passed over; where no
/// <see cref="PassedOver"/> is given (the search for derived types, whose caller's catch takes the library's failure)
/// the catch lets the failure go on to that caller.</remarks>
internal sealed class AnalyzerScope
{
    private const GetMemberOptions MemberDefinitions = GetMemberOptions.IgnoreInheritedMembers | GetMemberOptions.ReturnMemberDefinitions;

    public required AssemblyList List { get; init; }

    public required IReadOnlyList<LoadedModule> Assemblies { get; init; }

    /// <summary>The reads of the type systems of the modules the scope can reach, awaited before it was made
    /// (<see cref="ReadReachableAsync"/>): it scans those read, and notes those the library could not read in
    /// <see cref="Unreadable"/>.</summary>
    public required ModuleTypeSystems TypeSystems { get; init; }

    /// <summary>The modules of the scope the library could not read, noted as the scan reaches them, for the analysis
    /// to name beside what the others gave.</summary>
    public required ConcurrentDictionary<LoadedModule, UnreadableModule> Unreadable { private get; init; }

    /// <summary>Where a part the library fails reading is noted as passed over; null to let the failure go on to the
    /// caller.</summary>
    public required PassedOver? PassedOver { get; init; }

    public required Accessibility EffectiveAccessibility { get; init; }

    /// <summary>Whether the analyzed symbol is reachable only from inside <see cref="TypeScope"/>.</summary>
    public required bool IsLocal { get; init; }

    public required ITypeDefinition TypeScope { get; init; }

    /// <summary>The file <see cref="TypeScope"/> was read from.</summary>
    public required MetadataFile ScopeFile { get; init; }

    /// <summary>The metadata name of <see cref="TypeScope"/>, its arity after a backtick for a generic one: what a
    /// reference to it from another module names.</summary>
    public required string ScopeName { get; init; }

    /// <summary>The name of <see cref="TypeScope"/> as the library gives it, which the search of its friends looks for,
    /// as ILSpy's does.</summary>
    public required string ScopeTypeName { get; init; }

    public required string ScopeNamespace { get; init; }

    /// <summary>The reads of the type systems of every module a scope over <paramref name="list"/> can reach, each
    /// awaited: the listed modules', and <paramref name="own"/>'s, the module the analyzed symbol is in; each added to
    /// <paramref name="reading"/>, the unit's, one released (closed) before passed over.</summary>
    public static Task<ModuleTypeSystems> ReadReachableAsync(ModuleReadLease reading, AssemblyList list, LoadedModule own, CancellationToken ct)
    {
        return LoadedAssemblies.ReadTypeSystemsAsync(reading, list.Assemblies.Append(own).Distinct(), ct);
    }

    /// <summary>Where the symbols related to <paramref name="entity"/>, of the assemblies of the context's list, can be,
    /// scanning the type systems the context read and noting in the context what the library fails reading; null when
    /// it fails reading where <paramref name="entity"/> can be seen from, noted as a part of the analyzed module.</summary>
    public static AnalyzerScope? Create(AnalyzerContext context, IEntity entity)
    {
        ScopeFacts facts;
        try
        {
            facts = ReadScopeFacts(entity);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            context.PassedOver.Note(context.AnalyzedFile, e);
            return null;
        }

        return Make(context.AssemblyList, context.TypeSystems, context.Unreadable, context.PassedOver, facts);
    }

    /// <summary>Where the symbols related to <paramref name="entity"/>, of the assemblies of <paramref name="list"/>, can be,
    /// scanning <paramref name="typeSystems"/>, and noting in <paramref name="unreadable"/> the modules it reaches that the
    /// library could not read; what the library throws reading goes on to the caller.</summary>
    public static AnalyzerScope Create(AssemblyList list, ModuleTypeSystems typeSystems, ConcurrentDictionary<LoadedModule, UnreadableModule> unreadable,
        IEntity entity)
    {
        var facts = ReadScopeFacts(entity);
        return Make(list, typeSystems, unreadable, passedOver: null, facts);
    }

    private static AnalyzerScope Make(AssemblyList list, ModuleTypeSystems typeSystems, ConcurrentDictionary<LoadedModule, UnreadableModule> unreadable,
        PassedOver? passedOver, ScopeFacts facts)
    {
        var scopeName = facts.TypeParameterCount > 0 ? facts.Name + "`" + facts.TypeParameterCount : facts.Name;
        return new AnalyzerScope
        {
            List = list,
            // The modules read for it, the list as it stood then: one listed since was not read, and is not scanned.
            Assemblies = typeSystems.Modules,
            TypeSystems = typeSystems,
            Unreadable = unreadable,
            PassedOver = passedOver,
            EffectiveAccessibility = facts.Accessibility,
            TypeScope = facts.TypeScope,
            ScopeFile = facts.File,
            ScopeName = scopeName,
            ScopeTypeName = facts.Name,
            ScopeNamespace = facts.Namespace,
            IsLocal = facts.Accessibility.LessThanOrEqual(Accessibility.Private),
        };
    }

    // What the scope is made from, read out of the entity: how far it can be seen, by the library's walk out through the
    // types declaring it, and the type that bounds it, with its file and names.
    private static ScopeFacts ReadScopeFacts(IEntity entity)
    {
        DetermineEffectiveAccessibility(entity, out var typeScope, out var accessibility);
        return new ScopeFacts
        {
            Accessibility = accessibility, TypeScope = typeScope, File = DecompilerFacts.GetMetadataFile(typeScope), Name = typeScope.Name,
            TypeParameterCount = typeScope.TypeParameterCount, Namespace = typeScope.Namespace,
        };
    }

    private sealed class ScopeFacts
    {
        public required Accessibility Accessibility { get; init; }
        public required ITypeDefinition TypeScope { get; init; }
        public required MetadataFile File { get; init; }
        public required string Name { get; init; }
        public required int TypeParameterCount { get; init; }
        public required string Namespace { get; init; }
    }

    public IEnumerable<MetadataFile> GetModules(CancellationToken ct)
    {
        if (IsLocal)
        {
            return [ScopeFile];
        }

        if (EffectiveAccessibility.LessThanOrEqual(Accessibility.Internal))
        {
            return GetModuleAndAnyFriends(ct);
        }

        return GetReferencingModules(ct);
    }

    /// <summary>The type system of <paramref name="module"/> as read; null for one the library could not read, which is
    /// noted in <see cref="Unreadable"/>.</summary>
    public DecompilerTypeSystem? FindTypeSystem(MetadataFile module)
    {
        var read = TypeSystems.Read.GetValueOrDefault(module);
        if (read is not null)
        {
            return read;
        }

        NoteUnreadable(module);
        return null;
    }

    private void NoteUnreadable(MetadataFile module)
    {
        foreach (var unreadable in TypeSystems.Unreadable)
        {
            if (LoadedAssemblies.IsReadFrom(unreadable.Module, module))
            {
                Unreadable.TryAdd(unreadable.Module, unreadable);
                return;
            }
        }

        throw new InvalidOperationException($"The type system of {module.FileName} was not read before the scope scanned it.");
    }

    /// <summary>The type systems of the scope's modules the library could read, in the order of
    /// <see cref="GetModules"/>; those it could not are noted in <see cref="Unreadable"/>.</summary>
    public IEnumerable<DecompilerTypeSystem> ListTypeSystems(CancellationToken ct)
    {
        return ListScanned(ct).Select(static s => s.TypeSystem);
    }

    /// <summary>As <see cref="ListTypeSystems"/>, each with the file it was read from.</summary>
    public IEnumerable<(MetadataFile File, DecompilerTypeSystem TypeSystem)> ListScanned(CancellationToken ct)
    {
        foreach (var module in GetModules(ct))
        {
            var typeSystem = FindTypeSystem(module);
            if (typeSystem is not null)
            {
                yield return (module, typeSystem);
            }
        }
    }

    public IEnumerable<ITypeDefinition> GetTypes(CancellationToken ct)
    {
        return ListTypes(ct).Select(static t => t.Type);
    }

    /// <summary>The types in scope, each with the file it was read from: the scope's type and those nested in it when
    /// the scope is local, else every type of each module scanned.</summary>
    public IEnumerable<(MetadataFile File, ITypeDefinition Type)> ListTypes(CancellationToken ct)
    {
        if (IsLocal)
        {
            var nested = ReadNested(TypeScope, ScopeFile);
            foreach (var type in nested ?? [])
            {
                yield return (ScopeFile, type);
            }

            yield break;
        }

        foreach (var (file, typeSystem) in ListScanned(ct))
        {
            var types = ReadTypes(typeSystem, file);
            foreach (var type in types ?? [])
            {
                yield return (file, type);
            }
        }
    }

    // The type and those nested in it, by the library's walk; null when it fails, noted.
    private ITypeDefinition[]? ReadNested(ITypeDefinition type, MetadataFile file)
    {
        try
        {
            return TreeTraversal.PreOrder(type, static t => t.NestedTypes).ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException && PassedOver is not null)
        {
            PassedOver.Note(file, e);
            return null;
        }
    }

    // The types of the module; null when the library fails listing them, noted.
    private ITypeDefinition[]? ReadTypes(DecompilerTypeSystem typeSystem, MetadataFile file)
    {
        try
        {
            return typeSystem.MainModule.TypeDefinitions.ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException && PassedOver is not null)
        {
            PassedOver.Note(file, e);
            return null;
        }
    }

    /// <summary>The members in scope with code <paramref name="matches"/> accepts, type by type: each such method, then
    /// each property and event with such an accessor. A type's members are read under a catch of their own;
    /// <paramref name="matches"/>, the analysis's test, runs outside it, given the file the method was found in.</summary>
    public IEnumerable<IEntity> FindMembersWithCode(Func<MetadataFile, IMethod, bool> matches, CancellationToken ct)
    {
        foreach (var (file, type) in ListTypes(ct))
        {
            var members = ReadMembersWithCode(type, file);
            if (members is null)
            {
                continue;
            }

            foreach (var method in members.Methods)
            {
                if (matches(file, method))
                {
                    yield return method;
                }
            }
            foreach (var (member, accessors) in members.Accessed)
            {
                if (accessors.Any(a => matches(file, a)))
                {
                    yield return member;
                }
            }
        }
    }

    // The methods the type declares, then its properties and events with their accessors; none for a type of no file,
    // null when the library fails reading them, noted.
    private MembersWithCode? ReadMembersWithCode(ITypeDefinition type, MetadataFile file)
    {
        try
        {
            if (type.ParentModule?.MetadataFile is null)
            {
                return new MembersWithCode { Methods = [], Accessed = [] };
            }

            return new MembersWithCode
            {
                Methods = type.GetMembers(static m => m is IMethod, MemberDefinitions).OfType<IMethod>().ToArray(),
                Accessed = type.Properties.Concat<IMember>(type.Events).Select(static m => (m, Members.Accessors(m).ToArray())).ToArray(),
            };
        }
        catch (Exception e) when (e is not OperationCanceledException && PassedOver is not null)
        {
            PassedOver.Note(file, e);
            return null;
        }
    }

    private sealed class MembersWithCode
    {
        public required IMethod[] Methods { get; init; }

        public required (IMember Member, IMethod[] Accessors)[] Accessed { get; init; }
    }

    // The scope's own module, then each module that references the module the walk is at, by the app's resolution of
    // its references, and names the scope's type; on through each that forwards it. A module's references and its use
    // of the type are read under catches of their own, and the walk decides outside them.
    private IEnumerable<MetadataFile> GetReferencingModules(CancellationToken ct)
    {
        yield return ScopeFile;
        var toWalk = new Stack<MetadataFile>();
        var checkedFiles = new HashSet<MetadataFile> { ScopeFile };
        toWalk.Push(ScopeFile);
        do
        {
            var current = toWalk.Pop();
            foreach (var assembly in Assemblies)
            {
                ct.ThrowIfCancellationRequested();
                var module = LoadedAssemblies.GetFile(assembly);
                if (checkedFiles.Contains(module))
                {
                    continue;
                }

                var references = ReadAssemblyReferences(module);
                if (references is null || !references.Any(r => IsKnownAs(r, current)) || !checkedFiles.Add(module))
                {
                    continue;
                }

                var use = ReadScopeTypeUse(module);
                if (use is null)
                {
                    continue;
                }

                if (use.Value.References)
                {
                    yield return module;
                }

                if (use.Value.Forwards)
                {
                    toWalk.Push(module);
                }
            }
        } while (toWalk.Count > 0);
    }

    // Whether the app resolves reference to the module read from file.
    private bool IsKnownAs(AssemblyReference reference, MetadataFile file)
    {
        var known = List.FindKnown(reference, exactOrNewer: true, listedOnly: false);
        var knownFile = known is null ? null : LoadedAssemblies.GetFile(known);
        return knownFile == file;
    }

    // The assemblies an assembly references; null for a module that is no assembly, or when the library fails reading
    // them, noted.
    private AssemblyReference[]? ReadAssemblyReferences(MetadataFile module)
    {
        try
        {
            return module.IsAssembly ? module.AssemblyReferences.ToArray() : null;
        }
        catch (Exception e) when (e is not OperationCanceledException && PassedOver is not null)
        {
            PassedOver.Note(module, e);
            return null;
        }
    }

    // Whether the module references the scope's type, and whether it forwards it; null when the library fails reading
    // its tables, noted.
    private (bool References, bool Forwards)? ReadScopeTypeUse(MetadataFile module)
    {
        try
        {
            return (ReferencesScopeType(module.Metadata, ScopeName, ScopeNamespace), ForwardsScopeType(module.Metadata, ScopeName, ScopeNamespace));
        }
        catch (Exception e) when (e is not OperationCanceledException && PassedOver is not null)
        {
            PassedOver.Note(module, e);
            return null;
        }
    }

    // The scope's own module, then each of the list's modules it shows its internals to that references the scope's type.
    private IEnumerable<MetadataFile> GetModuleAndAnyFriends(CancellationToken ct)
    {
        yield return ScopeFile;
        var values = ReadFriendValues(ScopeFile);
        if (values is null)
        {
            yield break;
        }

        var friends = ListFriendNames(values);
        if (friends.Count == 0)
        {
            yield break;
        }

        foreach (var assembly in Assemblies)
        {
            ct.ThrowIfCancellationRequested();
            if (!friends.Contains(assembly.Name))
            {
                continue;
            }

            var file = LoadedAssemblies.GetFile(assembly);
            var references = ReadFriendUse(file);
            if (references == true)
            {
                yield return file;
            }
        }
    }

    // The value of each InternalsVisibleTo attribute of the module that is a string; null when the library fails reading
    // them, noted.
    private List<string>? ReadFriendValues(MetadataFile module)
    {
        try
        {
            var metadata = module.Metadata;
            var provider = MetadataExtensions.MinimalAttributeTypeProvider;
            var values = new List<string>();
            foreach (var handle in metadata.CustomAttributes)
            {
                var attribute = metadata.GetCustomAttribute(handle);
                if (!IsInternalsVisibleTo(metadata, attribute))
                {
                    continue;
                }

                var value = attribute.DecodeValue(provider);
                if (value.FixedArguments[0].Value is string name)
                {
                    values.Add(name);
                }
            }
            return values;
        }
        catch (Exception e) when (e is not OperationCanceledException && PassedOver is not null)
        {
            PassedOver.Note(module, e);
            return null;
        }
    }

    // The friends' names: each value up to its first comma, where its public key starts.
    private static HashSet<string> ListFriendNames(List<string> values)
    {
        var friends = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            friends.Add(value.Split(',')[0]);
        }
        return friends;
    }

    // Whether a friend with metadata references the scope's type; null when the library fails reading it, noted.
    private bool? ReadFriendUse(MetadataFile file)
    {
        try
        {
            return !file.IsMetadataOnly && ReferencesScopeType(file.Metadata, ScopeTypeName, ScopeNamespace);
        }
        catch (Exception e) when (e is not OperationCanceledException && PassedOver is not null)
        {
            PassedOver.Note(file, e);
            return null;
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
