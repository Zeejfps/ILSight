using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

public static partial class BrowseNodes
{
    /// <summary>Whether the type's metadata lists anything the node would: a base type, an interface,
    /// a nested type, a member, or types that may derive from it.</summary>
    private static bool CanListMembers(DefinedType type)
    {
        var definition = type.Module.File.Metadata.GetTypeDefinition(type.Handle);
        return !definition.BaseType.IsNil || definition.GetInterfaceImplementations().Count > 0 || definition.GetNestedTypes().Length > 0 ||
               DerivedTypes.CanHave(type.Symbol) || definition.GetFields().Count > 0 || definition.GetMethods().Count > 0 ||
               definition.GetProperties().Count > 0 || definition.GetEvents().Count > 0;
    }

    /// <summary>The base and derived types, then the nested types, fields, properties, events and methods,
    /// each sorted by name but an enum's fields, which keep their order; an enum's value field and the
    /// accessors are left out.</summary>
    private static BrowseNode[] ListMembers(BrowseNode parent, DefinedType definition)
    {
        var type = definition.Symbol;
        var module = definition.Module;
        var result = new List<BrowseNode>();
        if (type.DirectBaseTypes.Any())
        {
            result.Add(Create(parent, new NodeSubject.BaseTypes { Type = definition }));
        }

        if (DerivedTypes.CanHave(type))
        {
            result.Add(Create(parent, new NodeSubject.DerivedTypes { Type = definition }));
        }

        foreach (var nested in type.NestedTypes.OrderBy(t => t.Name, NaturalStringComparer.Instance))
        {
            AddIfListed(result, CreateForType(module, parent, nested));
        }

        var fields = type.Fields.Where(f => !(type.Kind == TypeKind.Enum && !f.IsStatic));
        if (type.Kind != TypeKind.Enum)
        {
            fields = fields.OrderBy(f => f.Name, NaturalStringComparer.Instance);
        }

        foreach (var field in fields)
        {
            AddIfListed(result, CreateForMember(module, parent, field));
        }

        foreach (var property in type.Properties.OrderBy(p => p.Name, NaturalStringComparer.Instance))
        {
            AddIfListed(result, CreateForMember(module, parent, property));
        }

        foreach (var @event in type.Events.OrderBy(e => e.Name, NaturalStringComparer.Instance))
        {
            AddIfListed(result, CreateForMember(module, parent, @event));
        }

        foreach (var method in type.Methods.Where(m => !m.IsAccessor).OrderBy(m => m.Name, NaturalStringComparer.Instance))
        {
            AddIfListed(result, CreateForMember(module, parent, method));
        }

        return result.ToArray();
    }

    private static BrowseNode[] ListAccessors(BrowseNode parent, DefinedMember definition)
    {
        var result = new List<BrowseNode>();
        foreach (var accessor in Members.Accessors(definition.Symbol))
        {
            AddIfListed(result, CreateForMember(definition.Module, parent, accessor));
        }

        return result.ToArray();
    }

    private static BrowseNode[] ListBaseTypes(BrowseNode parent, DefinedType type)
    {
        return type.Symbol.DirectBaseTypes
            .Select(t => Create(parent, new NodeSubject.BaseType { Value = NodeDefinitions.CreateBaseType(type.Module, t) }))
            .ToArray();
    }

    /// <summary>The types in scope of <paramref name="type"/> that derive from it directly, as
    /// <see cref="DerivedTypes"/> finds them, under <paramref name="parent"/>.</summary>
    private static BrowseNode[] FindDerivedTypes(LoadedModule module, BrowseNode parent, ITypeDefinition type)
    {
        var result = new List<BrowseNode>();
        foreach (var derived in DerivedTypes.Find(module.List, type, CancellationToken.None))
        {
            var info = NodeDefinitions.FindDerivedType(module, derived);
            if (info is not null)
            {
                result.Add(Create(parent, new NodeSubject.DerivedType { Value = info }));
            }
        }
        return result.ToArray();
    }
}
