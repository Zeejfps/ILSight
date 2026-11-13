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
        var file = LoadedAssemblies.GetFile(module);
        var result = new List<BrowseNode> { MetadataNodes.CreateMetadataFolder(module, parent, file, "Metadata") };
        var symbols = LoadedAssemblies.EnsureDebugSymbols(module);
        if (symbols is not null)
        {
            result.Add(MetadataNodes.CreateMetadataFolder(module, parent, symbols.File, symbols.Title));
        }

        result.Add(BrowseNodes.Create(parent, new NodeSubject.References { Owner = module }));
        if (!file.Resources.IsEmpty)
        {
            result.Add(BrowseNodes.Create(parent, new NodeSubject.Resources { Owner = module }));
        }

        var byNamespace = ListTypesByNamespace(main);
        var forwardedByNamespace = ForwardedTypes.ListByNamespace(file.Metadata);
        result.AddRange(ListNamespaceNodes(parent, module, byNamespace, forwardedByNamespace));
        return result.ToArray();
    }

    /// <summary>The top-level types of <paramref name="main"/> by their namespace, those missing left out.</summary>
    private static Dictionary<string, List<ITypeDefinition>> ListTypesByNamespace(MetadataModule main)
    {
        var byNamespace = new Dictionary<string, List<ITypeDefinition>>(StringComparer.Ordinal);
        foreach (var type in main.TopLevelTypeDefinitions)
        {
            if (Members.IsMissing(type))
            {
                continue;
            }

            if (!byNamespace.TryGetValue(type.Namespace, out var types))
            {
                byNamespace.Add(type.Namespace, types = new List<ITypeDefinition>());
            }

            types.Add(type);
        }
        return byNamespace;
    }

    /// <summary>A node for every namespace a type is in, defined or forwarded, or a name nests in, sorted by name, each
    /// knowing the one it nests in.</summary>
    private static List<BrowseNode> ListNamespaceNodes(BrowseNode parent, LoadedModule module, Dictionary<string, List<ITypeDefinition>> byNamespace,
        Dictionary<string, List<ForwardedType>> forwardedByNamespace)
    {
        var namespaces = new Dictionary<string, BrowseNode>(StringComparer.Ordinal);
        var nodes = new List<BrowseNode>();
        var names = byNamespace.Keys
            .Union(forwardedByNamespace.Keys)
            .SelectMany(NamespaceNames.AndEnclosing)
            .Distinct()
            .Order(NaturalStringComparer.Instance);
        foreach (var name in names)
        {
            var info = CreateNamespace(module, name, byNamespace.GetValueOrDefault(name, []), forwardedByNamespace.GetValueOrDefault(name, []));
            var node = BrowseNodes.Create(parent, new NodeSubject.Namespace { Value = info }, FindEnclosing(namespaces, name));
            namespaces.Add(name, node);
            nodes.Add(node);
        }
        return nodes;
    }

    /// <summary>The namespace <paramref name="name"/> of <paramref name="module"/>, with the types it defines and those it
    /// forwards.</summary>
    private static NamespaceInfo CreateNamespace(LoadedModule module, string name, List<ITypeDefinition> types, List<ForwardedType> forwarded)
    {
        var forwardedTypes = forwarded
            .Select(f => NodeDefinitions.CreateForwardedType(module, f))
            .ToList();
        return NodeDefinitions.CreateNamespace(module, name, types, forwardedTypes);
    }

    /// <summary>The namespace <paramref name="name"/> extends by a segment, made already, as names
    /// sort after the names they extend; null for an outermost name.</summary>
    private static BrowseNode? FindEnclosing(Dictionary<string, BrowseNode> namespaces, string name)
    {
        var enclosing = NamespaceNames.EnclosingName(name);
        if (enclosing is null)
        {
            return null;
        }

        return namespaces[enclosing];
    }

    /// <summary>The types the namespace defines and those it forwards, sorted together by name, then by
    /// how many type parameters they have.</summary>
    internal static BrowseNode[] ListNamespace(BrowseNode parent, NamespaceInfo ns)
    {
        var types = NodeDefinitions.GetTypes(ns);
        var children = new List<(string Name, int Arity, BrowseNode Node)>(types.Count + ns.Forwarded.Count);
        foreach (var type in types)
        {
            var node = BrowseNodes.CreateForType(ns.Module, parent, type);
            if (node is not null)
            {
                children.Add((type.Name, type.TypeParameterCount, node));
            }
        }
        foreach (var forwarded in ns.Forwarded)
        {
            children.Add((forwarded.SortName, forwarded.Arity, BrowseNodes.Create(parent, new NodeSubject.ForwardedType { Value = forwarded })));
        }

        return children
            .OrderBy(static c => c.Name, NaturalStringComparer.Instance)
            .ThenBy(static c => c.Arity)
            .Select(static c => c.Node)
            .ToArray();
    }
}
