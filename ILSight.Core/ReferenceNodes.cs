using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

internal static class ReferenceNodes
{
    private const string LocalFunctionMarker = ">g__";

    internal static bool HasReferences(LoadedModule module)
    {
        var file = LoadedAssemblies.GetFile(module);
        if (!file.AssemblyReferences.IsEmpty)
        {
            return true;
        }

        return file.Metadata.GetTableRowCount(TableIndex.ModuleRef) > 0;
    }

    /// <summary>The references of <paramref name="module"/>, sorted by name, under <paramref name="parent"/>.
    /// What the module uses of them is read here, off the UI thread, so that listing what each holds
    /// never reads it there.</summary>
    internal static BrowseNode[] ListAssemblyReferences(LoadedModule module, BrowseNode parent)
    {
        var references = ListingReads.ValueOf(ReadAssemblyReferences(module));
        return references
            .OrderBy(static r => r.Name, NaturalStringComparer.Instance)
            .Select(r => BrowseNodes.CreateListed(parent, new NodeSubject.AssemblyReference { Value = new AssemblyReferenceInfo { Module = module, Reference = r.Reference } }))
            .ToArray();
    }

    private static Outcome<(ICSharpCode.Decompiler.Metadata.AssemblyReference Reference, string Name)[]> ReadAssemblyReferences(LoadedModule module)
    {
        try
        {
            LoadedAssemblies.EnsureReferenceUsage(module);
            return new Outcome<(ICSharpCode.Decompiler.Metadata.AssemblyReference Reference, string Name)[]>.Ok(LoadedAssemblies.GetFile(module).AssemblyReferences.Select(static r => (r, r.Name)).ToArray());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(ICSharpCode.Decompiler.Metadata.AssemblyReference Reference, string Name)[]>.Failed(e);
        }
    }

    /// <summary>The types the module uses from the reference, then the references of the assembly it
    /// resolves to, found where following it finds it but not listed; only the types when it resolves
    /// to none.</summary>
    internal static BrowseNode[] ListReferenceContents(BrowseNode parent, AssemblyReferenceInfo reference)
    {
        var children = new List<BrowseNode>();
        var usesTypes = ListingReads.ValueOf(ReadUsesTypes(reference));
        if (usesTypes)
        {
            children.Add(BrowseNodes.CreateListed(parent, new NodeSubject.ReferencedTypes { Reference = reference }));
        }

        var referenced = ListingReads.ValueOf(ResolveReferenced(reference));
        if (referenced is not null)
        {
            children.AddRange(ListAssemblyReferences(referenced, parent));
        }

        return children.ToArray();
    }

    // Whether the module uses types of the reference.
    private static Outcome<bool> ReadUsesTypes(AssemblyReferenceInfo reference)
    {
        try
        {
            return new Outcome<bool>.Ok(ForwardedTypes.UsesReference(LoadedAssemblies.EnsureReferenceUsage(reference.Module), reference.Reference.Handle));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<bool>.Failed(e);
        }
    }

    private static Outcome<LoadedModule?> ResolveReferenced(AssemblyReferenceInfo reference)
    {
        try
        {
            return new Outcome<LoadedModule?>.Ok(LoadedAssemblies.GetResolver(reference.Module).ResolveAssembly(reference.Reference));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<LoadedModule?>.Failed(e);
        }
    }

    /// <summary>The types the module uses from <paramref name="reference"/>, sorted by namespace, then
    /// name, and after them the types it forwards there.</summary>
    internal static BrowseNode[] ListUsedTypes(BrowseNode parent, AssemblyReferenceInfo reference)
    {
        var module = reference.Module;
        var (usedNames, forwarders) = ListingReads.ValueOf(ReadUsedTypes(reference));
        var used = usedNames
            .Select(n => CreateUsedTypeInfo(parent, module, n))
            .OrderBy(static i => i.Namespace, NaturalStringComparer.Instance)
            .ThenBy(static i => i.Label, NaturalStringComparer.Instance)
            .Select(i => BrowseNodes.CreateListed(parent, new NodeSubject.TypeReference { Value = i }));
        var forwarded = forwarders
            .OrderBy(static f => f.Namespace, NaturalStringComparer.Instance)
            .ThenBy(static f => f.Label, NaturalStringComparer.Instance)
            .Select(f => BrowseNodes.CreateListed(parent, new NodeSubject.ForwardedType { Value = NodeDefinitions.CreateForwardedType(module, f) }));
        return used
            .Concat(forwarded)
            .ToArray();
    }

    // The names of the types the module uses from the reference, and the types it forwards there.
    private static Outcome<(UsedTypeName[] Used, ForwardedType[] Forwarded)> ReadUsedTypes(AssemblyReferenceInfo reference)
    {
        try
        {
            var usage = LoadedAssemblies.EnsureReferenceUsage(reference.Module);
            var metadata = LoadedAssemblies.GetFile(reference.Module).Metadata;
            var handle = reference.Reference.Handle;
            var used = ForwardedTypes.ListTypesIn(usage, handle).Select(h => ReadUsedTypeName(metadata, h)).ToArray();
            var forwarded = ForwardedTypes.ListForwardedTo(usage, handle).Select(h => ForwardedTypes.Read(metadata, h)).ToArray();
            return new Outcome<(UsedTypeName[] Used, ForwardedType[] Forwarded)>.Ok((used, forwarded));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(UsedTypeName[] Used, ForwardedType[] Forwarded)>.Failed(e);
        }
    }

    // A used type's handle and the names its row holds.
    private readonly record struct UsedTypeName
    {
        public required TypeReferenceHandle Handle { get; init; }

        public required string Namespace { get; init; }

        public required string Name { get; init; }
    }

    private static UsedTypeName ReadUsedTypeName(MetadataReader metadata, TypeReferenceHandle handle)
    {
        var reference = metadata.GetTypeReference(handle);
        return new UsedTypeName { Handle = handle, Namespace = metadata.GetString(reference.Namespace), Name = metadata.GetString(reference.Name) };
    }

    // The used type as the tree names it: its label, with its arity written as C# writes it, and its names qualified by
    // the type or namespace it sits in.
    private static TypeReferenceInfo CreateUsedTypeInfo(BrowseNode parent, LoadedModule module, UsedTypeName used)
    {
        var bare = ReflectionHelper.SplitTypeParameterCountFromReflectionName(used.Name, out var arity);
        var label = ForwardedTypes.LabelOf(bare, arity);
        return new TypeReferenceInfo
        {
            Module = module, Handle = used.Handle, Namespace = used.Namespace, Label = label, FullName = GetQualifiedName(parent, used.Namespace, label),
            ReflectionName = GetQualifiedReflectionName(parent, used.Namespace, used.Name),
        };
    }

    private static string GetQualifiedName(BrowseNode parent, string ns, string label)
    {
        if (parent.Subject is NodeSubject.TypeReference)
        {
            return $"{parent.FullName}.{label}";
        }

        if (ns.Length == 0)
        {
            return label;
        }

        return $"{ns}.{label}";
    }

    private static string GetQualifiedReflectionName(BrowseNode parent, string ns, string name)
    {
        if (parent.Subject is NodeSubject.TypeReference holder)
        {
            return $"{holder.Value.ReflectionName}+{name}";
        }

        if (ns.Length == 0)
        {
            return name;
        }

        return $"{ns}.{name}";
    }

    internal static bool UsesAnythingOf(TypeReferenceInfo type)
    {
        var usage = LoadedAssemblies.EnsureReferenceUsage(type.Module);
        var types = ForwardedTypes.ListTypesIn(usage, type.Handle);
        if (types.Count > 0)
        {
            return true;
        }

        return ForwardedTypes.ListMembersOf(usage, type.Handle).Count > 0;
    }

    /// <summary>The types nested in the used type and the members of it the module uses, each sorted by
    /// label; a member used under several type arguments once, and overloads whose labels match each.</summary>
    internal static BrowseNode[] ListUsedOf(BrowseNode parent, TypeReferenceInfo type, MetadataModule module)
    {
        var (nestedNames, usedMembers) = ListingReads.ValueOf(ReadUsedOf(type, module));
        var nested = nestedNames
            .Select(n => BrowseNodes.CreateListed(parent, new NodeSubject.TypeReference { Value = CreateUsedTypeInfo(parent, type.Module, n) }))
            .OrderBy(static n => n.Text, NaturalStringComparer.Instance);
        var members = usedMembers
            .Select(m => CreateUsedMember(parent, type.Module, m))
            .DistinctBy(static n => n.Segment)
            .OrderBy(static n => n.Text, NaturalStringComparer.Instance);
        return nested
            .Concat(members)
            .ToArray();
    }

    // The names of the types nested in the used type, and each member of it the module uses: its handle, the member it
    // declares, and that member's label and shape.
    private static Outcome<(UsedTypeName[] Nested, UsedMember[] Members)> ReadUsedOf(TypeReferenceInfo type, MetadataModule module)
    {
        try
        {
            var usage = LoadedAssemblies.EnsureReferenceUsage(type.Module);
            var metadata = LoadedAssemblies.GetFile(type.Module).Metadata;
            var nested = ForwardedTypes.ListTypesIn(usage, type.Handle).Select(h => ReadUsedTypeName(metadata, h)).ToArray();
            var members = ForwardedTypes.ListMembersOf(usage, type.Handle).Select(h => ReadUsedMember(module, h)).ToArray();
            return new Outcome<(UsedTypeName[] Nested, UsedMember[] Members)>.Ok((nested, members));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(UsedTypeName[] Nested, UsedMember[] Members)>.Failed(e);
        }
    }

    private sealed record UsedMember
    {
        public required MemberReferenceHandle Handle { get; init; }

        public required IMember Declared { get; init; }

        public required string Label { get; init; }

        public required MemberShape Shape { get; init; }
    }

    private static UsedMember ReadUsedMember(MetadataModule module, MemberReferenceHandle handle)
    {
        var declared = ResolveDeclared(module, handle);
        return new UsedMember { Handle = handle, Declared = declared, Label = EntityText.Label(declared), Shape = EntityText.ShapeOf(declared) };
    }

    private static BrowseNode CreateUsedMember(BrowseNode parent, LoadedModule module, UsedMember used)
    {
        var info = LibraryObjects<MemberReferenceInfo, IMember>.Keep(new MemberReferenceInfo
        {
            Module = module, Handle = used.Handle, FullName = $"{parent.FullName}.{used.Label}", Shape = used.Shape,
        }, used.Declared);
        return BrowseNodes.CreateListed(parent, new NodeSubject.MemberReference { Value = info });
    }

    private static IMember ResolveDeclared(MetadataModule module, MemberReferenceHandle handle)
    {
        var declared = ResolveUsed(module, handle).MemberDefinition;
        if (declared is IMethod method && method.AccessorOwner is not null)
        {
            return method.AccessorOwner;
        }

        return declared;
    }

    private static IMember ResolveUsed(MetadataModule module, MemberReferenceHandle handle)
    {
        var reference = module.MetadataFile.Metadata.GetMemberReference(handle);
        var kind = reference.GetKind();
        if (kind == MemberReferenceKind.Field)
        {
            return DecompilerFacts.ResolveMember(module, handle);
        }

        return module.ResolveMethod(handle, default);
    }

    /// <summary>The modules <paramref name="module"/> references, sorted by name, under <paramref name="parent"/>.
    /// Which methods import from each is read here, off the UI thread, so that their rows can tell whether
    /// they have children without reading it there.</summary>
    internal static BrowseNode[] ListModuleReferences(LoadedModule module, BrowseNode parent)
    {
        var references = ListingReads.ValueOf(ReadModuleReferences(module));
        return references
            .Select(r => BrowseNodes.CreateListed(parent, new NodeSubject.ModuleReference { Value = r }))
            .OrderBy(static n => n.Text, NaturalStringComparer.Instance)
            .ToArray();
    }

    private static Outcome<ModuleReferenceInfo[]> ReadModuleReferences(LoadedModule module)
    {
        try
        {
            var metadata = LoadedAssemblies.GetFile(module).Metadata;
            var imports = ListImportsByModule(metadata);
            return new Outcome<ModuleReferenceInfo[]>.Ok(Enumerable.Range(1, metadata.GetTableRowCount(TableIndex.ModuleRef))
                .Select(MetadataTokens.ModuleReferenceHandle)
                .Select(h => ReadModuleReference(module, metadata, h, imports))
                .ToArray());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<ModuleReferenceInfo[]>.Failed(e);
        }
    }

    private static ModuleReferenceInfo ReadModuleReference(LoadedModule module, MetadataReader metadata, ModuleReferenceHandle handle,
        Dictionary<ModuleReferenceHandle, List<MethodDefinitionHandle>> imports)
    {
        var name = metadata.GetString(metadata.GetModuleReference(handle).Name);
        return new ModuleReferenceInfo { Module = module, Name = name, ContainsMetadata = IsModuleWithMetadata(metadata, name), Imports = ListImportsFrom(imports, handle) };
    }

    private static bool IsModuleWithMetadata(MetadataReader metadata, string name)
    {
        foreach (var handle in metadata.AssemblyFiles)
        {
            var file = metadata.GetAssemblyFile(handle);
            if (metadata.StringComparer.Equals(file.Name, name))
            {
                return file.ContainsMetadata;
            }
        }
        return false;
    }

    private static Dictionary<ModuleReferenceHandle, List<MethodDefinitionHandle>> ListImportsByModule(MetadataReader metadata)
    {
        var imports = new Dictionary<ModuleReferenceHandle, List<MethodDefinitionHandle>>();
        foreach (var handle in metadata.MethodDefinitions)
        {
            var module = metadata.GetMethodDefinition(handle).GetImport().Module;
            if (module.IsNil)
            {
                continue;
            }

            if (!imports.TryGetValue(module, out var methods))
            {
                methods = new List<MethodDefinitionHandle>();
                imports.Add(module, methods);
            }
            methods.Add(handle);
        }
        return imports;
    }

    private static IReadOnlyList<MethodDefinitionHandle> ListImportsFrom(Dictionary<ModuleReferenceHandle, List<MethodDefinitionHandle>> imports,
        ModuleReferenceHandle module)
    {
        if (imports.TryGetValue(module, out var methods))
        {
            return methods;
        }

        return [];
    }

    /// <summary>The methods imported from the module, sorted by label, then by the type declaring each.</summary>
    internal static BrowseNode[] ListImports(BrowseNode parent, ModuleReferenceInfo module, MetadataModule typeSystem)
    {
        var imports = new List<ImportedMethodInfo>();
        foreach (var handle in module.Imports)
        {
            var method = ListingReads.ValueOf(ReadImport(typeSystem, handle, module));
            var import = CreateImport(method, module);
            if (import is not null)
            {
                imports.Add(import);
            }
        }
        return imports
            .OrderBy(static i => i.Label, NaturalStringComparer.Instance)
            .ThenBy(static i => i.DeclaringTypeName, NaturalStringComparer.Instance)
            .Select(i => BrowseNodes.CreateListed(parent, new NodeSubject.ImportedMethod { Value = i }))
            .ToArray();
    }

    // What the tree reads of a method importing from the module: its name, label, id and entry point, and the type
    // declaring it with that type's name, none for a method declared by no type.
    private sealed record ImportRead
    {
        public required IMethod Method { get; init; }

        public required string Name { get; init; }

        public required string Label { get; init; }

        public required string Id { get; init; }

        public required string EntryPoint { get; init; }

        public required ITypeDefinition? Declaring { get; init; }

        public required string? DeclaringTypeName { get; init; }
    }

    private static Outcome<ImportRead> ReadImport(MetadataModule typeSystem, MethodDefinitionHandle handle, ModuleReferenceInfo module)
    {
        try
        {
            var method = typeSystem.GetDefinition(handle);
            var declaring = method.DeclaringTypeDefinition;
            return new Outcome<ImportRead>.Ok(new ImportRead
            {
                Method = method,
                Name = method.Name,
                Label = EntityText.Label(method),
                Id = IdStringProvider.GetIdString(method),
                EntryPoint = ReadEntryPoint(LoadedAssemblies.GetFile(module.Module).Metadata, method),
                Declaring = declaring,
                DeclaringTypeName = declaring is null ? null : EntityText.FullName(declaring),
            });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<ImportRead>.Failed(e);
        }
    }

    // A stub the compiler generates as a local function, as [LibraryImport] does, is labelled and goes as the method
    // declaring it, with the stub's own parameters, the ones the module is called with.
    private static ImportedMethodInfo? CreateImport(ImportRead read, ModuleReferenceInfo module)
    {
        if (read.Declaring is null || read.DeclaringTypeName is null)
        {
            return null;
        }

        var enclosing = FindEnclosingName(read.Name);
        var shown = ListingReads.ValueOf(ReadShown(module.Module, read.Method, read.Declaring, enclosing));
        if (shown is null)
        {
            return null;
        }

        var label = enclosing is null ? read.Label : enclosing + read.Label.Substring(read.Name.Length);
        return new ImportedMethodInfo
        {
            Shown = shown, Id = read.Id, Label = label, DeclaringTypeName = read.DeclaringTypeName, EntryPoint = read.EntryPoint, ModuleName = module.Name,
        };
    }

    // The compiler writes the name of the method a local function is declared in into the function's name, e.g.
    // CloseHandle in <CloseHandle>g____PInvoke|277_0.
    private static string? FindEnclosingName(string name)
    {
        if (!name.StartsWith('<'))
        {
            return null;
        }

        var end = name.IndexOf(LocalFunctionMarker, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        return name.Substring(1, end - 1);
    }

    private static Outcome<DefinedMember?> ReadShown(LoadedModule module, IMethod method, ITypeDefinition declaring, string? enclosing)
    {
        try
        {
            return new Outcome<DefinedMember?>.Ok(NodeDefinitions.FindMember(module, GetShownMethod(method, declaring, enclosing)));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<DefinedMember?>.Failed(e);
        }
    }

    // The method shown for an import: the one named enclosing that declaring declares, where the import is a local
    // function's stub; else the import itself.
    private static IMethod GetShownMethod(IMethod method, ITypeDefinition declaring, string? enclosing)
    {
        if (enclosing is null)
        {
            return method;
        }

        var declared = declaring.Methods.FirstOrDefault(m => m.Name == enclosing);
        if (declared is null)
        {
            return method;
        }

        return declared;
    }

    // The runtime looks the entry point up by the name the method's import gives, else by the method's own.
    private static string ReadEntryPoint(MetadataReader metadata, IMethod method)
    {
        var handle = DecompilerFacts.GetMethodHandle(method);
        var import = metadata.GetMethodDefinition(handle).GetImport();
        if (import.Name.IsNil)
        {
            return method.Name;
        }

        return metadata.GetString(import.Name);
    }
}
