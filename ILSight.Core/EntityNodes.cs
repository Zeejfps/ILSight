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
        var members = ListingReads.ValueOf(ReadMembers(definition));
        var module = definition.Module;
        var result = new List<BrowseNode>();
        if (members.HasBaseTypes)
        {
            result.Add(BrowseNodes.CreateListed(parent, new NodeSubject.BaseTypes { Type = definition }));
        }

        if (members.CanHaveDerived)
        {
            result.Add(BrowseNodes.CreateListed(parent, new NodeSubject.DerivedTypes { Type = definition }));
        }

        foreach (var nested in members.Nested.OrderBy(static t => t.Name, NaturalStringComparer.Instance))
        {
            AddIfListed(result, BrowseNodes.CreateListedType(module, parent, nested.Type));
        }

        var isEnum = members.Kind == TypeKind.Enum;
        var fields = members.Fields.Where(f => !(isEnum && !f.IsStatic));
        if (!isEnum)
        {
            fields = fields.OrderBy(static f => f.Name, NaturalStringComparer.Instance);
        }

        AddMembers(result, module, parent, fields.Select(static f => f.Member));
        AddMembers(result, module, parent, members.Properties.OrderBy(static p => p.Name, NaturalStringComparer.Instance).Select(static p => p.Member));
        AddMembers(result, module, parent, members.Events.OrderBy(static e => e.Name, NaturalStringComparer.Instance).Select(static e => e.Member));
        AddMembers(result, module, parent, members.Methods.Where(static m => !m.IsAccessor).OrderBy(static m => m.Name, NaturalStringComparer.Instance)
            .Select(static m => m.Member));
        return result.ToArray();
    }

    // What a type's members listing reads of it, each value a sort or a filter needs read out before the app's rules
    // run on them.
    private sealed record TypeMembers
    {
        public required TypeKind Kind { get; init; }

        public required bool HasBaseTypes { get; init; }

        public required bool CanHaveDerived { get; init; }

        public required IReadOnlyList<(ITypeDefinition Type, string Name)> Nested { get; init; }

        public required IReadOnlyList<(IMember Member, string Name, bool IsStatic)> Fields { get; init; }

        public required IReadOnlyList<(IMember Member, string Name)> Properties { get; init; }

        public required IReadOnlyList<(IMember Member, string Name)> Events { get; init; }

        public required IReadOnlyList<(IMember Member, string Name, bool IsAccessor)> Methods { get; init; }
    }

    private static Outcome<TypeMembers> ReadMembers(DefinedType definition)
    {
        try
        {
            var type = NodeDefinitions.GetSymbol(definition);
            return new Outcome<TypeMembers>.Ok(new TypeMembers
            {
                Kind = type.Kind,
                HasBaseTypes = type.DirectBaseTypes.Any(),
                CanHaveDerived = DerivedTypes.CanHave(type),
                Nested = type.NestedTypes.Select(static t => (t, t.Name)).ToArray(),
                Fields = type.Fields.Select(static f => ((IMember)f, f.Name, f.IsStatic)).ToArray(),
                Properties = type.Properties.Select(static p => ((IMember)p, p.Name)).ToArray(),
                Events = type.Events.Select(static e => ((IMember)e, e.Name)).ToArray(),
                Methods = type.Methods.Select(static m => ((IMember)m, m.Name, m.IsAccessor)).ToArray(),
            });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<TypeMembers>.Failed(e);
        }
    }

    private static void AddMembers(List<BrowseNode> nodes, LoadedModule module, BrowseNode parent, IEnumerable<IMember> members)
    {
        foreach (var member in members)
        {
            AddIfListed(nodes, BrowseNodes.CreateListedMember(module, parent, member));
        }
    }

    internal static BrowseNode[] ListAccessors(BrowseNode parent, DefinedMember definition)
    {
        var accessors = ListingReads.ValueOf(ReadAccessors(definition));
        var result = new List<BrowseNode>();
        AddMembers(result, definition.Module, parent, accessors);
        return result.ToArray();
    }

    private static Outcome<IMember[]> ReadAccessors(DefinedMember definition)
    {
        try
        {
            return new Outcome<IMember[]>.Ok(Members.Accessors(NodeDefinitions.GetSymbol(definition)).ToArray<IMember>());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<IMember[]>.Failed(e);
        }
    }

    internal static BrowseNode[] ListBaseTypes(BrowseNode parent, DefinedType type)
    {
        var baseTypes = ListingReads.ValueOf(ReadBaseTypes(type));
        return baseTypes
            .Select(b => BrowseNodes.CreateListed(parent, new NodeSubject.BaseType { Value = b }))
            .ToArray();
    }

    private static Outcome<BaseTypeInfo[]> ReadBaseTypes(DefinedType type)
    {
        try
        {
            return new Outcome<BaseTypeInfo[]>.Ok(NodeDefinitions.GetSymbol(type).DirectBaseTypes.Select(b => NodeDefinitions.CreateBaseType(type.Module, b)).ToArray());
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<BaseTypeInfo[]>.Failed(e);
        }
    }

    /// <summary>The types in scope of <paramref name="type"/> that derive from it directly, as
    /// <see cref="DerivedTypes"/> finds them, then a node naming each module the search reached that the library could
    /// not read, under <paramref name="parent"/>.</summary>
    internal static BrowseNode[] FindDerivedTypes(LoadedModule module, BrowseNode parent, ITypeDefinition type, ChildrenReads reads)
    {
        var (found, unreadable) = ListingReads.ValueOf(SearchDerived(module, type, reads.RequireReachable()));
        var result = new List<BrowseNode>();
        foreach (var info in found)
        {
            if (info is not null)
            {
                result.Add(BrowseNodes.CreateListed(parent, new NodeSubject.DerivedType { Value = info }));
            }
        }

        foreach (var missed in unreadable)
        {
            result.Add(BrowseNodes.CreateListed(parent, new NodeSubject.UnreadableModule { Value = missed }));
        }

        return result.ToArray();
    }

    // The search, each type found read as the node's info (none for one with no metadata token, which the tree does not
    // list), and the modules it could not read.
    private static Outcome<(DerivedTypeInfo?[] Found, UnreadableModule[] Unreadable)> SearchDerived(LoadedModule module, ITypeDefinition type,
        ModuleTypeSystems reachable)
    {
        try
        {
            var search = DerivedTypes.Search(module.List, reachable, type, CancellationToken.None);
            var found = search.Found.Select(d => NodeDefinitions.FindDerivedType(module, d)).ToArray();
            return new Outcome<(DerivedTypeInfo?[] Found, UnreadableModule[] Unreadable)>.Ok((found, search.Unreadable.ToArray()));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(DerivedTypeInfo?[] Found, UnreadableModule[] Unreadable)>.Failed(e);
        }
    }

    private static void AddIfListed(List<BrowseNode> nodes, BrowseNode? node)
    {
        if (node is not null)
        {
            nodes.Add(node);
        }
    }
}
