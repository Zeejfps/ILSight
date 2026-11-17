using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

internal static class ModuleNodes
{
    /// <summary>The root of <paramref name="module"/>'s tree: at the top, or for one read out of a package
    /// under the folder holding its file.</summary>
    internal static BrowseNode CreateModuleRoot(LoadedModule module, BrowseNode? parent)
    {
        return BrowseNodes.Create(parent, new NodeSubject.Module { Value = module });
    }

    /// <summary>The metadata, the debug metadata when there is some, the references and the resources,
    /// then every namespace a type is in or a name nests in, flat and sorted by name, each knowing the
    /// one it nests in.</summary>
    internal static BrowseNode[] ListModule(BrowseNode parent, LoadedModule module, MetadataModule main)
    {
        var contents = ListingReads.ValueOf(ReadContents(module, main));
        var result = new List<BrowseNode> { MetadataNodes.CreateMetadataFolder(module, parent, contents.File, "Metadata") };
        if (contents.Symbols is not null)
        {
            result.Add(MetadataNodes.CreateMetadataFolder(module, parent, contents.Symbols.File, contents.Symbols.Title));
        }

        result.Add(BrowseNodes.CreateListed(parent, new NodeSubject.References { Owner = module }));
        if (contents.HasResources)
        {
            result.Add(BrowseNodes.CreateListed(parent, new NodeSubject.Resources { Owner = module }));
        }

        var byNamespace = GroupTypesByNamespace(contents.Types);
        var forwardedByNamespace = contents.Forwarded.ToLookup(static f => f.Namespace, StringComparer.Ordinal);
        result.AddRange(ListNamespaceNodes(parent, module, byNamespace, forwardedByNamespace));
        return result.ToArray();
    }

    // What a module's listing reads of it: its file, its debug symbols, whether it holds resources, its top-level
    // types with each one's namespace, and the types it forwards.
    private sealed record ModuleContents
    {
        public required MetadataFile File { get; init; }

        public required DebugSymbols? Symbols { get; init; }

        public required bool HasResources { get; init; }

        public required IReadOnlyList<(ITypeDefinition Type, string Namespace, bool HasMetadata)> Types { get; init; }

        public required IReadOnlyList<ForwardedType> Forwarded { get; init; }
    }

    private static Outcome<ModuleContents> ReadContents(LoadedModule module, MetadataModule main)
    {
        try
        {
            var file = LoadedAssemblies.GetFile(module);
            return new Outcome<ModuleContents>.Ok(new ModuleContents
            {
                File = file,
                Symbols = LoadedAssemblies.EnsureDebugSymbols(module),
                HasResources = !file.Resources.IsEmpty,
                Types = main.TopLevelTypeDefinitions.Select(static t => (t, t.Namespace, !Members.HasNoMetadata(t))).ToArray(),
                Forwarded = ForwardedTypes.ListForwarders(file.Metadata),
            });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<ModuleContents>.Failed(e);
        }
    }

    // The types the tree lists (those with metadata), by namespace.
    private static Dictionary<string, List<ITypeDefinition>> GroupTypesByNamespace(IReadOnlyList<(ITypeDefinition Type, string Namespace, bool HasMetadata)> types)
    {
        var byNamespace = new Dictionary<string, List<ITypeDefinition>>(StringComparer.Ordinal);
        foreach (var (type, ns, hasMetadata) in types)
        {
            if (!hasMetadata)
            {
                continue;
            }

            if (!byNamespace.TryGetValue(ns, out var grouped))
            {
                byNamespace.Add(ns, grouped = new List<ITypeDefinition>());
            }

            grouped.Add(type);
        }
        return byNamespace;
    }

    private static List<BrowseNode> ListNamespaceNodes(BrowseNode parent, LoadedModule module, Dictionary<string, List<ITypeDefinition>> byNamespace,
        ILookup<string, ForwardedType> forwardedByNamespace)
    {
        var namespaces = new Dictionary<string, BrowseNode>(StringComparer.Ordinal);
        var nodes = new List<BrowseNode>();
        var names = byNamespace.Keys
            .Union(forwardedByNamespace.Select(static g => g.Key))
            .SelectMany(NamespaceNames.AndEnclosing)
            .Distinct()
            .Order(NaturalStringComparer.Instance);
        foreach (var name in names)
        {
            var info = CreateNamespace(module, name, byNamespace.GetValueOrDefault(name, []), forwardedByNamespace[name].ToList());
            var node = BrowseNodes.CreateListed(parent, new NodeSubject.Namespace { Value = info }, FindEnclosing(namespaces, name));
            namespaces.Add(name, node);
            nodes.Add(node);
        }
        return nodes;
    }

    private static NamespaceInfo CreateNamespace(LoadedModule module, string name, List<ITypeDefinition> types, List<ForwardedType> forwarded)
    {
        var forwardedTypes = forwarded
            .Select(f => NodeDefinitions.CreateForwardedType(module, f))
            .ToList();
        var (anyPublic, allGenerated) = ListingReads.ValueOf(ReadNamespaceFlags(types));
        return NodeDefinitions.CreateNamespace(module, name, types, forwardedTypes, isPublicApi: forwardedTypes.Count > 0 || anyPublic,
            isCompilerGenerated: forwardedTypes.Count == 0 && allGenerated);
    }

    // Whether a type of the namespace is public API and whether all of them are compiler-generated, each read stopping
    // as soon as a type answers.
    private static Outcome<(bool AnyPublic, bool AllGenerated)> ReadNamespaceFlags(List<ITypeDefinition> types)
    {
        try
        {
            return new Outcome<(bool AnyPublic, bool AllGenerated)>.Ok((types.Any(EntityText.IsPublicApi), types.All(Members.IsCompilerGenerated)));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(bool AnyPublic, bool AllGenerated)>.Failed(e);
        }
    }

    private static BrowseNode? FindEnclosing(Dictionary<string, BrowseNode> namespaces, string name)
    {
        var enclosing = NamespaceNames.EnclosingName(name);
        if (enclosing is null)
        {
            return null;
        }

        // Made already, as names sort after the names they extend.
        return namespaces[enclosing];
    }

    /// <summary>The types the namespace defines and those it forwards, sorted together by name, then by
    /// how many type parameters they have.</summary>
    internal static BrowseNode[] ListNamespace(BrowseNode parent, NamespaceInfo ns)
    {
        var types = ListingReads.ValueOf(ReadNamespaceTypes(ns));
        var children = new List<(string Name, int Arity, BrowseNode Node)>(types.Length + ns.Forwarded.Count);
        foreach (var (type, name, arity) in types)
        {
            var node = BrowseNodes.CreateListedType(ns.Module, parent, type);
            if (node is not null)
            {
                children.Add((name, arity, node));
            }
        }
        foreach (var forwarded in ns.Forwarded)
        {
            children.Add((forwarded.SortName, forwarded.Arity, BrowseNodes.CreateListed(parent, new NodeSubject.ForwardedType { Value = forwarded })));
        }

        return children
            .OrderBy(static c => c.Name, NaturalStringComparer.Instance)
            .ThenBy(static c => c.Arity)
            .Select(static c => c.Node)
            .ToArray();
    }

    // The namespace's own types, each with its name and how many type parameters it has.
    private static Outcome<(ITypeDefinition Type, string Name, int Arity)[]> ReadNamespaceTypes(NamespaceInfo ns)
    {
        try
        {
            return new Outcome<(ITypeDefinition Type, string Name, int Arity)[]>.Ok(NodeDefinitions.GetTypes(ns).Select(static t => (t, t.Name, t.TypeParameterCount)).ToArray());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(ITypeDefinition Type, string Name, int Arity)[]>.Failed(e);
        }
    }
}
