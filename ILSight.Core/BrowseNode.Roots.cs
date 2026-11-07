using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

public static partial class BrowseNodes
{
    /// <summary>The root of <paramref name="module"/>'s tree: at the top, or for one read out of a package
    /// under the folder holding its file.</summary>
    internal static BrowseNode CreateModuleRoot(LoadedModule module, BrowseNode? parent)
    {
        return Create(parent, new NodeSubject.Module { Value = module });
    }

    /// <summary>The root of a listed file that could not be read, without children, showing why.</summary>
    internal static BrowseNode CreateFailedRoot(FailedAssembly failed)
    {
        return Create(null, new NodeSubject.FailedAssembly { Value = failed });
    }

    /// <summary>The metadata, the debug metadata when there is some, the references and the resources,
    /// then every namespace a type is in or a name nests in, flat and sorted by name, each knowing the
    /// one it nests in.</summary>
    private static BrowseNode[] ListModule(BrowseNode parent, LoadedModule module)
    {
        var file = module.File;
        var result = new List<BrowseNode> { CreateMetadataFolder(module, parent, file, "Metadata") };
        var symbols = LoadedAssemblies.EnsureDebugSymbols(module);
        if (symbols != null)
            result.Add(CreateMetadataFolder(module, parent, symbols.File, symbols.Title));
        result.Add(Create(parent, new NodeSubject.References { Owner = module }));
        if (!file.Resources.IsEmpty)
            result.Add(Create(parent, new NodeSubject.Resources { Owner = module }));

        var byNamespace = new Dictionary<string, List<ITypeDefinition>>(StringComparer.Ordinal);
        foreach (var type in LoadedAssemblies.EnsureMainModule(module).TopLevelTypeDefinitions)
        {
            if (Members.IsMissing(type)) continue;
            if (!byNamespace.TryGetValue(type.Namespace, out var types))
                byNamespace.Add(type.Namespace, types = new List<ITypeDefinition>());
            types.Add(type);
        }
        var forwardedByNamespace = ForwardedTypes.ListByNamespace(file.Metadata);

        var namespaces = new Dictionary<string, BrowseNode>(StringComparer.Ordinal);
        var names = byNamespace.Keys
            .Union(forwardedByNamespace.Keys)
            .SelectMany(NamespaceNames.AndEnclosing)
            .Distinct()
            .Order(NaturalStringComparer.Instance);
        foreach (var name in names)
        {
            var types = byNamespace.GetValueOrDefault(name, []);
            var forwarded = forwardedByNamespace.GetValueOrDefault(name, [])
                .Select(f => NodeDefinitions.CreateForwardedType(module, f))
                .ToList();
            var info = NodeDefinitions.CreateNamespace(module, name, types, forwarded);
            var node = Create(parent, new NodeSubject.Namespace { Value = info }, FindEnclosing(namespaces, name));
            namespaces.Add(name, node);
            result.Add(node);
        }
        return result.ToArray();
    }

    /// <summary>The namespace <paramref name="name"/> extends by a segment, made already, as names
    /// sort after the names they extend; null for an outermost name.</summary>
    private static BrowseNode? FindEnclosing(Dictionary<string, BrowseNode> namespaces, string name)
    {
        var enclosing = NamespaceNames.EnclosingName(name);
        if (enclosing == null)
            return null;
        return namespaces[enclosing];
    }

    /// <summary>The types the namespace defines and those it forwards, sorted together by name, then by
    /// how many type parameters they have.</summary>
    private static BrowseNode[] ListNamespace(BrowseNode parent, NamespaceInfo ns)
    {
        var children = new List<(string Name, int Arity, BrowseNode Node)>(ns.Types.Count + ns.Forwarded.Count);
        foreach (var type in ns.Types)
        {
            var node = CreateForType(ns.Module, parent, type);
            if (node != null)
                children.Add((type.Name, type.TypeParameterCount, node));
        }
        foreach (var forwarded in ns.Forwarded)
            children.Add((forwarded.SortName, forwarded.Arity, Create(parent, new NodeSubject.ForwardedType { Value = forwarded })));
        return children
            .OrderBy(static c => c.Name, NaturalStringComparer.Instance)
            .ThenBy(static c => c.Arity)
            .Select(static c => c.Node)
            .ToArray();
    }

    /// <summary>The module's name, version and the framework it was built for. The framework picked to
    /// resolve its references against is left out, so that the label, by which paths find the node, stays
    /// the same when another is picked.</summary>
    private static string GetModuleLabel(LoadedModule module)
    {
        var details = new[] { module.Version?.ToString(), GetBuiltForName(module) }.OfType<string>().ToArray();
        return details.Length == 0 ? module.Name : $"{module.Name} ({string.Join(", ", details)})";
    }

    private static string? GetBuiltForName(LoadedModule module)
    {
        if (module.DetectedTargetFramework == null)
            return null;
        return module.DetectedTargetFramework.Readable;
    }
}
