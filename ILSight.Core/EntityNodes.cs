using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

internal static class EntityNodes
{
    /// <summary>Whether the type's metadata lists anything the node would: a base type, an interface,
    /// a nested type, a member, or types that may derive from it.</summary>
    internal static bool CanListMembers(DefinedType type)
    {
        var definition = LoadedAssemblies.GetFile(type.Module).Metadata.GetTypeDefinition(type.Handle);
        return !definition.BaseType.IsNil || definition.GetInterfaceImplementations().Count > 0 || definition.GetNestedTypes().Length > 0 ||
               DerivedTypes.CanHave(NodeDefinitions.GetSymbol(type)) || definition.GetFields().Count > 0 || definition.GetMethods().Count > 0 ||
               definition.GetProperties().Count > 0 || definition.GetEvents().Count > 0;
    }

    /// <summary>The base and derived types, then the nested types, fields, properties, events and methods,
    /// each sorted by name but an enum's fields, which keep their order; an enum's value field and the
    /// accessors are left out.</summary>
    internal static BrowseNode[] ListMembers(BrowseNode parent, DefinedType definition)
    {
        var type = NodeDefinitions.GetSymbol(definition);
        var module = definition.Module;
        var result = new List<BrowseNode>();
        if (type.DirectBaseTypes.Any())
        {
            result.Add(BrowseNodes.Create(parent, new NodeSubject.BaseTypes { Type = definition }));
        }

        if (DerivedTypes.CanHave(type))
        {
            result.Add(BrowseNodes.Create(parent, new NodeSubject.DerivedTypes { Type = definition }));
        }

        foreach (var nested in type.NestedTypes.OrderBy(t => t.Name, NaturalStringComparer.Instance))
        {
            AddIfListed(result, BrowseNodes.CreateForType(module, parent, nested));
        }

        var fields = type.Fields.Where(f => !(type.Kind == TypeKind.Enum && !f.IsStatic));
        if (type.Kind != TypeKind.Enum)
        {
            fields = fields.OrderBy(f => f.Name, NaturalStringComparer.Instance);
        }

        foreach (var field in fields)
        {
            AddIfListed(result, BrowseNodes.CreateForMember(module, parent, field));
        }

        foreach (var property in type.Properties.OrderBy(p => p.Name, NaturalStringComparer.Instance))
        {
            AddIfListed(result, BrowseNodes.CreateForMember(module, parent, property));
        }

        foreach (var @event in type.Events.OrderBy(e => e.Name, NaturalStringComparer.Instance))
        {
            AddIfListed(result, BrowseNodes.CreateForMember(module, parent, @event));
        }

        foreach (var method in type.Methods.Where(m => !m.IsAccessor).OrderBy(m => m.Name, NaturalStringComparer.Instance))
        {
            AddIfListed(result, BrowseNodes.CreateForMember(module, parent, method));
        }

        return result.ToArray();
    }

    internal static BrowseNode[] ListAccessors(BrowseNode parent, DefinedMember definition)
    {
        var result = new List<BrowseNode>();
        foreach (var accessor in Members.Accessors(NodeDefinitions.GetSymbol(definition)))
        {
            AddIfListed(result, BrowseNodes.CreateForMember(definition.Module, parent, accessor));
        }

        return result.ToArray();
    }

    internal static BrowseNode[] ListBaseTypes(BrowseNode parent, DefinedType type)
    {
        return NodeDefinitions.GetSymbol(type).DirectBaseTypes
            .Select(t => BrowseNodes.Create(parent, new NodeSubject.BaseType { Value = NodeDefinitions.CreateBaseType(type.Module, t) }))
            .ToArray();
    }

    /// <summary>The types in scope of <paramref name="type"/> that derive from it directly, as
    /// <see cref="DerivedTypes"/> finds them, under <paramref name="parent"/>.</summary>
    internal static BrowseNode[] FindDerivedTypes(LoadedModule module, BrowseNode parent, ITypeDefinition type, ChildrenReads reads)
    {
        var result = new List<BrowseNode>();
        foreach (var derived in DerivedTypes.Find(module.List, reads.RequireReachable(), reads.Unreadable, type, CancellationToken.None))
        {
            var info = NodeDefinitions.FindDerivedType(module, derived);
            if (info is not null)
            {
                result.Add(BrowseNodes.Create(parent, new NodeSubject.DerivedType { Value = info }));
            }
        }
        return result.ToArray();
    }

    private static void AddIfListed(List<BrowseNode> nodes, BrowseNode? node)
    {
        if (node is not null)
        {
            nodes.Add(node);
        }
    }
}
