using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

public static partial class BrowseNodes
{
    private const string LocalFunctionMarker = ">g__";

    private static bool HasReferences(LoadedModule module)
    {
        if (!module.File.AssemblyReferences.IsEmpty)
            return true;
        return module.File.Metadata.GetTableRowCount(TableIndex.ModuleRef) > 0;
    }

    /// <summary>The references of <paramref name="module"/>, sorted by name, under <paramref name="parent"/>.
    /// What the module uses of them is read here, off the UI thread, so that listing what each holds
    /// never reads it there.</summary>
    internal static BrowseNode[] ListAssemblyReferences(LoadedModule module, BrowseNode parent)
    {
        LoadedAssemblies.EnsureReferenceUsage(module);
        return module.File.AssemblyReferences
            .OrderBy(r => r.Name, NaturalStringComparer.Instance)
            .Select(r => Create(parent, new NodeSubject.AssemblyReference { Value = new AssemblyReferenceInfo { Module = module, Reference = r } }))
            .ToArray();
    }

    /// <summary>The types the module uses from the reference, then the references of the assembly it
    /// resolves to, found where following it finds it but not listed; only the types when it resolves
    /// to none.</summary>
    private static BrowseNode[] ListReferenceContents(BrowseNode parent, AssemblyReferenceInfo reference)
    {
        var children = new List<BrowseNode>();
        if (ForwardedTypes.UsesReference(LoadedAssemblies.EnsureReferenceUsage(reference.Module), reference.Reference.Handle))
            children.Add(Create(parent, new NodeSubject.ReferencedTypes { Reference = reference }));
        var referenced = ResolveReferenced(reference);
        if (referenced != null)
            children.AddRange(ListAssemblyReferences(referenced, parent));
        return children.ToArray();
    }

    /// <summary>The assembly <paramref name="reference"/> resolves to, loaded if it is not; null when it
    /// cannot be found or read.</summary>
    private static LoadedModule? ResolveReferenced(AssemblyReferenceInfo reference)
    {
        if (AssemblyResolving.ResolveAssembly(reference.Module.Resolver, reference.Reference) is not LoadedModule referenced)
            return null;
        return referenced;
    }

    /// <summary>The types the module uses from <paramref name="reference"/>, sorted by namespace, then
    /// name, and after them the types it forwards there.</summary>
    private static BrowseNode[] ListUsedTypes(BrowseNode parent, AssemblyReferenceInfo reference)
    {
        var module = reference.Module;
        var usage = LoadedAssemblies.EnsureReferenceUsage(module);
        var metadata = module.File.Metadata;
        var handle = reference.Reference.Handle;
        var used = ForwardedTypes.ListTypesIn(usage, handle)
            .Select(h => CreateUsedTypeInfo(parent, module, h))
            .OrderBy(i => i.Namespace, NaturalStringComparer.Instance)
            .ThenBy(i => i.Label, NaturalStringComparer.Instance)
            .Select(i => Create(parent, new NodeSubject.TypeReference { Value = i }));
        var forwarded = ForwardedTypes.ListForwardedTo(usage, handle)
            .Select(h => ForwardedTypes.Read(metadata, h))
            .OrderBy(f => f.Namespace, NaturalStringComparer.Instance)
            .ThenBy(f => f.Label, NaturalStringComparer.Instance)
            .Select(f => (BrowseNode)CreateForwarded(module, parent, f));
        return used
            .Concat(forwarded)
            .ToArray();
    }

    /// <summary>The node of a type forwarded to another assembly, under <paramref name="parent"/>.</summary>
    internal static BrowseNode CreateForwarded(LoadedModule module, BrowseNode parent, ForwardedType type)
    {
        return Create(parent, new NodeSubject.ForwardedType { Value = NodeDefinitions.CreateForwardedType(module, type) });
    }

    /// <summary>The node of the type <paramref name="handle"/> names, used from a reference, under <paramref name="parent"/>.</summary>
    private static BrowseNode CreateUsedType(BrowseNode parent, LoadedModule module, TypeReferenceHandle handle)
    {
        return Create(parent, new NodeSubject.TypeReference { Value = CreateUsedTypeInfo(parent, module, handle) });
    }

    /// <summary>The type <paramref name="handle"/> names, used from a reference, as listed under <paramref name="parent"/>.</summary>
    private static TypeReferenceInfo CreateUsedTypeInfo(BrowseNode parent, LoadedModule module, TypeReferenceHandle handle)
    {
        var metadata = module.File.Metadata;
        var reference = metadata.GetTypeReference(handle);
        var name = metadata.GetString(reference.Name);
        var bare = ReflectionHelper.SplitTypeParameterCountFromReflectionName(name, out var arity);
        var label = ForwardedTypes.LabelOf(bare, arity);
        var ns = metadata.GetString(reference.Namespace);
        return new TypeReferenceInfo { Module = module, Handle = handle, Namespace = ns, Label = label, FullName = GetQualifiedName(parent, ns, label), ReflectionName = GetQualifiedReflectionName(parent, ns, name) };
    }

    /// <summary>The full name of a type labelled <paramref name="label"/> listed under <paramref name="parent"/>: after the
    /// type it is nested in, when this node is one, else after its namespace.</summary>
    private static string GetQualifiedName(BrowseNode parent, string ns, string label)
    {
        if (parent.Subject is NodeSubject.TypeReference)
            return $"{GetFullName(parent)}.{label}";
        if (ns.Length == 0)
            return label;
        return $"{ns}.{label}";
    }

    /// <summary>The reflection name of a type metadata names <paramref name="name"/> listed under <paramref name="parent"/>:
    /// after the type it is nested in, when this node is one, else after its namespace.</summary>
    private static string GetQualifiedReflectionName(BrowseNode parent, string ns, string name)
    {
        if (parent.Subject is NodeSubject.TypeReference holder)
            return $"{holder.Value.ReflectionName}+{name}";
        if (ns.Length == 0)
            return name;
        return $"{ns}.{name}";
    }

    private static bool UsesAnythingOf(TypeReferenceInfo type)
    {
        var usage = LoadedAssemblies.EnsureReferenceUsage(type.Module);
        if (ForwardedTypes.ListTypesIn(usage, type.Handle).Count > 0)
            return true;
        return ForwardedTypes.ListMembersOf(usage, type.Handle).Count > 0;
    }

    /// <summary>The types nested in the used type and the members of it the module uses, each sorted by
    /// label; a member used under several type arguments once, and overloads whose labels match each.</summary>
    private static BrowseNode[] ListUsedOf(BrowseNode parent, TypeReferenceInfo type)
    {
        var usage = LoadedAssemblies.EnsureReferenceUsage(type.Module);
        var module = LoadedAssemblies.EnsureMainModule(type.Module);
        var nested = ForwardedTypes.ListTypesIn(usage, type.Handle)
            .Select(h => CreateUsedType(parent, type.Module, h))
            .OrderBy(n => n.Text, NaturalStringComparer.Instance)
            .Cast<BrowseNode>();
        var members = ForwardedTypes.ListMembersOf(usage, type.Handle)
            .Select(h => CreateUsedMember(parent, type.Module, h, ResolveDeclared(module, h)))
            .DistinctBy(n => BrowseNodes.GetSegment(n))
            .OrderBy(n => n.Text, NaturalStringComparer.Instance);
        return nested
            .Concat(members)
            .ToArray();
    }

    private static BrowseNode CreateUsedMember(BrowseNode parent, LoadedModule module, MemberReferenceHandle handle, IMember declared)
    {
        var info = new MemberReferenceInfo { Module = module, Handle = handle, Declared = declared, FullName = $"{GetFullName(parent)}.{EntityText.Label(declared)}" };
        return Create(parent, new NodeSubject.MemberReference { Value = info });
    }

    /// <summary>The member <paramref name="handle"/> uses as its type declares it: for an accessor, the
    /// property or event it belongs to.</summary>
    private static IMember ResolveDeclared(MetadataModule module, MemberReferenceHandle handle)
    {
        var declared = ResolveUsed(module, handle).MemberDefinition;
        if (declared is IMethod method && method.AccessorOwner != null)
            return method.AccessorOwner;
        return declared;
    }

    private static IMember ResolveUsed(MetadataModule module, MemberReferenceHandle handle)
    {
        var reference = module.MetadataFile.Metadata.GetMemberReference(handle);
        if (reference.GetKind() == MemberReferenceKind.Field)
            return DecompilerFacts.ResolveMember(module, handle);
        return module.ResolveMethod(handle, default);
    }

    /// <summary>The modules <paramref name="module"/> references, sorted by name, under <paramref name="parent"/>.
    /// Which methods import from each is read here, off the UI thread, so that their rows can tell whether
    /// they have children without reading it there.</summary>
    private static BrowseNode[] ListModuleReferences(LoadedModule module, BrowseNode parent)
    {
        var metadata = module.File.Metadata;
        var imports = ListImportsByModule(metadata);
        return Enumerable.Range(1, metadata.GetTableRowCount(TableIndex.ModuleRef))
            .Select(MetadataTokens.ModuleReferenceHandle)
            .Select(h => CreateModuleReference(module, parent, h, imports))
            .OrderBy(n => n.Text, NaturalStringComparer.Instance)
            .Cast<BrowseNode>()
            .ToArray();
    }

    private static BrowseNode CreateModuleReference(LoadedModule module, BrowseNode parent, ModuleReferenceHandle handle,
        Dictionary<ModuleReferenceHandle, List<MethodDefinitionHandle>> imports)
    {
        var metadata = module.File.Metadata;
        var name = metadata.GetString(metadata.GetModuleReference(handle).Name);
        var info = new ModuleReferenceInfo { Module = module, Name = name, ContainsMetadata = IsModuleWithMetadata(metadata, name), Imports = ListImportsFrom(imports, handle) };
        return Create(parent, new NodeSubject.ModuleReference { Value = info });
    }

    /// <summary>Whether the assembly's file table lists the module as one holding metadata.</summary>
    private static bool IsModuleWithMetadata(MetadataReader metadata, string name)
    {
        foreach (var handle in metadata.AssemblyFiles)
        {
            var file = metadata.GetAssemblyFile(handle);
            if (metadata.StringComparer.Equals(file.Name, name))
                return file.ContainsMetadata;
        }
        return false;
    }

    /// <summary>The methods imported from each module, read from what each method's import names.</summary>
    private static Dictionary<ModuleReferenceHandle, List<MethodDefinitionHandle>> ListImportsByModule(MetadataReader metadata)
    {
        var imports = new Dictionary<ModuleReferenceHandle, List<MethodDefinitionHandle>>();
        foreach (var handle in metadata.MethodDefinitions)
        {
            var module = metadata.GetMethodDefinition(handle).GetImport().Module;
            if (module.IsNil)
                continue;
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
            return methods;
        return [];
    }

    /// <summary>The methods imported from the module, sorted by label, then by the type declaring each.</summary>
    private static BrowseNode[] ListImports(BrowseNode parent, ModuleReferenceInfo module)
    {
        var typeSystem = LoadedAssemblies.EnsureMainModule(module.Module);
        var imports = new List<ImportedMethodInfo>();
        foreach (var handle in module.Imports)
        {
            var import = CreateImport(typeSystem.GetDefinition(handle), module);
            if (import != null)
                imports.Add(import);
        }
        return imports
            .OrderBy(i => i.Label, NaturalStringComparer.Instance)
            .ThenBy(i => i.DeclaringTypeName, NaturalStringComparer.Instance)
            .Select(i => Create(parent, new NodeSubject.ImportedMethod { Value = i }))
            .ToArray();
    }

    /// <summary>The import of <paramref name="method"/>: labelled as the tree labels the method, beside the
    /// type declaring it. A stub the compiler generates as a local function, as <c>[LibraryImport]</c>
    /// does, is labelled and goes as the method declaring it, with the stub's own parameters, the ones
    /// the module is called with. Null for a method no type declares or with no address.</summary>
    private static ImportedMethodInfo? CreateImport(IMethod method, ModuleReferenceInfo module)
    {
        var declaring = method.DeclaringTypeDefinition;
        if (declaring == null)
            return null;
        var shown = NodeDefinitions.FindMember(module.Module, FindEnclosingMethod(method, declaring));
        if (shown == null)
            return null;
        var entryPoint = ReadEntryPoint(module.Module.File.Metadata, method);
        return new ImportedMethodInfo { Shown = shown, Id = IdStringProvider.GetIdString(method), Label = GetImportLabel(method), DeclaringTypeName = EntityText.FullName(declaring), EntryPoint = entryPoint, ModuleName = module.Name };
    }

    /// <summary>The tree's label for <paramref name="method"/>, named as the method enclosing it when it is a local function.</summary>
    private static string GetImportLabel(IMethod method)
    {
        var label = EntityText.Label(method);
        var enclosing = FindEnclosingName(method.Name);
        if (enclosing == null)
            return label;
        return enclosing + label.Substring(method.Name.Length);
    }

    /// <summary>The name of the method a local function is declared in, as the compiler writes it into the
    /// function's name, e.g. <c>CloseHandle</c> for <c>&lt;CloseHandle&gt;g____PInvoke|277_0</c>; null
    /// for a method that is no local function.</summary>
    private static string? FindEnclosingName(string name)
    {
        if (!name.StartsWith('<'))
            return null;
        var end = name.IndexOf(LocalFunctionMarker, StringComparison.Ordinal);
        if (end < 0)
            return null;
        return name.Substring(1, end - 1);
    }

    /// <summary>The method <paramref name="method"/> is declared in when it is a local function and its type
    /// declares a method of that name, else <paramref name="method"/> itself.</summary>
    private static IMethod FindEnclosingMethod(IMethod method, ITypeDefinition declaring)
    {
        var enclosing = FindEnclosingName(method.Name);
        if (enclosing == null)
            return method;
        var declared = declaring.Methods.FirstOrDefault(m => m.Name == enclosing);
        if (declared == null)
            return method;
        return declared;
    }

    /// <summary>The name the method's import gives, else the method's own, as the runtime looks it up.</summary>
    private static string ReadEntryPoint(MetadataReader metadata, IMethod method)
    {
        var handle = DecompilerFacts.GetMethodHandle(method);
        var import = metadata.GetMethodDefinition(handle).GetImport();
        if (import.Name.IsNil)
            return method.Name;
        return metadata.GetString(import.Name);
    }
}
