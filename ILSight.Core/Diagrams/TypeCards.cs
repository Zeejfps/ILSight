using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Diagrams;

/// <summary>Types read as diagram cards from the type system, alone, with the types of their assembly
/// they are related to, or as the types deriving from a type. Each read loads the assemblies it needs
/// without listing them: call them off the UI thread.</summary>
public static class TypeCards
{
    /// <summary>The card of the type <paramref name="type"/> points at; not found when it points at no type,
    /// or that cannot be resolved; unreadable when the library could not read the type system of an assembly
    /// resolving it reads.</summary>
    public static async Task<Lookup<TypeCard>> ReadAsync(AssemblyList list, CodeReference.Entity type, CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        var definition = await AssemblyListing.FindTypeDefinitionAsync(list, reading, type, ct);
        return ReadFound(definition);
    }

    /// <summary>The card of the type <paramref name="type"/> points at, then the cards of the types of its
    /// own assembly it is directly related to: those it derives from, implements or holds, and those
    /// deriving from it or implementing it. None when it points at no type; the module, with why, when the library
    /// could not read the type system of an assembly resolving it reads.</summary>
    public static async Task<TypeSystemWork<List<TypeCard>>> ReadWithNeighboursAsync(AssemblyList list, CodeReference.Entity type,
        CancellationToken cancellationToken)
    {
        using var reading = LoadedAssemblies.BeginReading();
        var definition = await AssemblyListing.FindTypeDefinitionAsync(list, reading, type, cancellationToken);
        return definition.Match<TypeSystemWork<List<TypeCard>>>(
            found => new TypeSystemWork<List<TypeCard>>.Done { Value = ReadWithNeighbours(found.Value, cancellationToken) },
            static _ => new TypeSystemWork<List<TypeCard>>.Done { Value = [] },
            static unreadable => new TypeSystemWork<List<TypeCard>>.Unreadable { Module = unreadable.Module });
    }

    private static List<TypeCard> ReadWithNeighbours(ITypeDefinition definition, CancellationToken cancellationToken)
    {
        var card = Read(definition);
        if (card is null)
        {
            return [];
        }

        var cards = new List<TypeCard> { card };
        foreach (var neighbour in ListNeighbours(definition))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = Read(neighbour);
            if (read is not null)
            {
                cards.Add(read);
            }
        }
        return cards;
    }

    /// <summary>The card of the type <paramref name="anchor"/> names; not found when its assembly cannot be
    /// read or no longer defines it; unreadable when the library could not read the assembly's type system.</summary>
    public static async Task<Lookup<TypeCard>> ReadAsync(AssemblyList list, TypeAnchor anchor, CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        var definition = await AssemblyListing.FindTypeDefinitionAsync(list, reading, anchor, ct);
        return ReadFound(definition);
    }

    private static Lookup<TypeCard> ReadFound(Lookup<ITypeDefinition> definition)
    {
        return definition.Match(
            static found => Lookup.FromNullable(Read(found.Value)),
            static _ => new Lookup<TypeCard>.NotFound(),
            static unreadable => new Lookup<TypeCard>.Unreadable { Module = unreadable.Module });
    }

    /// <summary>The cards of the types deriving directly from, or implementing, the type
    /// <paramref name="type"/> points at, among the assemblies <paramref name="list"/> holds now, once the reads of
    /// the type systems the search can reach ended, beside each module the search reached that the library could not
    /// read; none when it points at no type; no cards and its module when the library could not read the type system
    /// of an assembly resolving it reads.</summary>
    public static async Task<ScopeSearch<TypeCard>> ReadDerivedAsync(AssemblyList list, CodeReference.Entity type, CancellationToken cancellationToken)
    {
        using var reading = LoadedAssemblies.BeginReading();
        var definition = await AssemblyListing.FindTypeDefinitionAsync(list, reading, type, cancellationToken);
        return await definition.Match(
            found => ReadDerivedAsync(list, reading, found.Value, cancellationToken),
            static _ => Task.FromResult(new ScopeSearch<TypeCard> { Found = [], Unreadable = [] }),
            static unreadable => Task.FromResult(new ScopeSearch<TypeCard> { Found = [], Unreadable = [unreadable.Module] }));
    }

    private static async Task<ScopeSearch<TypeCard>> ReadDerivedAsync(AssemblyList list, ModuleReading reading, ITypeDefinition definition,
        CancellationToken cancellationToken)
    {
        var module = AssemblyListing.GetLoadedFrom(list, DecompilerFacts.GetMetadataFile(definition));
        var reachable = await DerivedTypes.ReadReachableAsync(reading, module, cancellationToken);
        var search = DerivedTypes.Search(list, reachable, definition, cancellationToken);
        var cards = search.Found
            .Where(IsShown)
            .Select(Read)
            .OfType<TypeCard>()
            .ToList();
        return new ScopeSearch<TypeCard> { Found = cards, Unreadable = search.Unreadable };
    }

    /// <summary>The card of <paramref name="type"/>; null when it has no metadata file or token to be
    /// referenced by, as a type the decompiler made up has not.</summary>
    internal static TypeCard? Read(ITypeDefinition type)
    {
        var reference = CodeReferences.Find(type);
        if (reference is null)
        {
            return null;
        }

        return new TypeCard
        {
            Type = reference,
            Anchor = new TypeAnchor { AssemblyPath = reference.FilePath, FullName = type.ReflectionName },
            Name = EntityText.Label(type),
            FullName = EntityText.FullName(type),
            AssemblyPath = reference.FilePath,
            Shape = EntityText.ShapeOf(type),
            Accessibility = EntityText.Map(type.Accessibility),
            Stereotype = StereotypeOf(type),
            Sections = ReadSections(type),
            Inherited = ReadInherited(type),
            Links = ReadLinks(type, reference),
            CanHaveDerived = DerivedTypes.CanHave(type),
        };
    }

    private static TypeStereotype StereotypeOf(ITypeDefinition type)
    {
        if (TypeFacts.IsInterface(type))
        {
            return TypeStereotype.Interface;
        }

        if (type.Kind != TypeKind.Class)
        {
            return TypeStereotype.None;
        }

        if (!type.IsAbstract)
        {
            return TypeStereotype.None;
        }

        if (type.IsSealed)
        {
            return TypeStereotype.Static;
        }

        return TypeStereotype.Abstract;
    }

    private static List<CardSection> ReadSections(ITypeDefinition type)
    {
        var sections = new List<CardSection>();
        AddSection(sections, MemberSection.Fields, ListFields(type));
        AddSection(sections, MemberSection.Properties, Sort(type.Properties));
        AddSection(sections, MemberSection.Methods, Sort(ListMethods(type)));
        AddSection(sections, MemberSection.Events, Sort(type.Events));
        return sections;
    }

    private static void AddSection(List<CardSection> sections, MemberSection kind, IEnumerable<IMember> members)
    {
        var shown = members
            .Where(IsShown)
            .Select(ToCardMember)
            .OfType<CardMember>()
            .ToList();
        if (shown.Count == 0)
        {
            return;
        }

        sections.Add(new CardSection { Kind = kind, Members = shown });
    }

    // As the tree lists them: an enum's values in the order declared, without its one instance field, which holds the
    // value.
    private static IEnumerable<IMember> ListFields(ITypeDefinition type)
    {
        if (type.Kind != TypeKind.Enum)
        {
            return Sort(type.Fields);
        }

        return type.Fields.Where(static f => f.IsStatic);
    }

    private static IEnumerable<IMethod> ListMethods(ITypeDefinition type)
    {
        return type.Methods.Where(static m => !m.IsAccessor);
    }

    private static IEnumerable<IMember> Sort(IEnumerable<IMember> members)
    {
        return members.OrderBy(static m => m.Name, NaturalStringComparer.Instance);
    }

    private static bool IsShown(IEntity entity)
    {
        return !Members.IsHidden(entity);
    }

    private static CardMember? ToCardMember(IMember member)
    {
        var reference = CodeReferences.Find(member);
        if (reference == null)
        {
            return null;
        }

        return new CardMember
        {
            Reference = reference,
            Text = EntityText.Label(member),
            Shape = EntityText.ShapeOf(member),
            Accessibility = EntityText.Map(member.Accessibility),
        };
    }

    // Nearest first: the library lists base types from object down. For an interface, they are the interfaces it
    // extends.
    private static List<InheritedGroup> ReadInherited(ITypeDefinition type)
    {
        return type.GetNonInterfaceBaseTypes()
            .Where(t => TypeFacts.IsAncestor(type, t))
            .Reverse()
            .Select(ReadInheritedFrom)
            .Where(static g => g.Members.Count > 0)
            .ToList();
    }

    private static InheritedGroup ReadInheritedFrom(IType ancestor)
    {
        var definition = ancestor.GetDefinition();
        if (definition is null)
        {
            return new InheritedGroup { From = EntityText.ShortTypeName(ancestor), AssemblyPath = null, Members = [] };
        }

        var members = definition.Fields
            .Concat<IMember>(definition.Properties)
            .Concat(ListMethods(definition))
            .Concat(definition.Events)
            .Where(IsInherited)
            .OrderBy(static m => m.Name, NaturalStringComparer.Instance)
            .Select(ToCardMember)
            .OfType<CardMember>()
            .ToList();
        return new InheritedGroup
        {
            From = EntityText.ShortTypeName(ancestor),
            AssemblyPath = CodeReferences.Find(definition)?.FilePath,
            Members = members,
        };
    }

    private static bool IsInherited(IMember member)
    {
        if (!IsShown(member))
        {
            return false;
        }

        if (member.IsStatic)
        {
            return false;
        }

        if (member.Accessibility == Accessibility.Private)
        {
            return false;
        }

        if (member is IMethod method)
        {
            return IsInheritedMethod(method);
        }

        return true;
    }

    private static bool IsInheritedMethod(IMethod method)
    {
        if (method.IsConstructor)
        {
            return false;
        }

        return !TypeFacts.OverridesObject(method);
    }

    private static List<TypeLink> ReadLinks(ITypeDefinition type, CodeReference.Entity from)
    {
        return ListRelations(type)
            .Select(r => LinkOf(from, r))
            .OfType<TypeLink>()
            .Distinct()
            .ToList();
    }

    private static TypeLink? LinkOf(CodeReference.Entity from, Relation relation)
    {
        var to = CodeReferences.Find(relation.Target);
        if (to == null)
        {
            return null;
        }

        return relation.ToLink(from, to);
    }

    private static List<Relation> ListRelations(ITypeDefinition type)
    {
        var relations = new List<Relation>();
        AddBases(relations, type);
        AddHeld(relations, type);
        return relations;
    }

    private static void AddBases(List<Relation> relations, ITypeDefinition type)
    {
        foreach (var baseType in type.DirectBaseTypes)
        {
            var definition = baseType.GetDefinition();
            if (definition is null)
            {
                continue;
            }

            if (IsImplicitBase(definition))
            {
                continue;
            }

            relations.Add(new Relation { Target = definition, ToLink = BaseLinkOf(type, definition) });
        }
    }

    private static bool IsImplicitBase(ITypeDefinition definition)
    {
        return definition.KnownTypeCode is KnownTypeCode.Object or KnownTypeCode.ValueType or KnownTypeCode.Enum;
    }

    private static Func<CodeReference.Entity, CodeReference.Entity, TypeLink> BaseLinkOf(ITypeDefinition type, ITypeDefinition baseType)
    {
        if (!TypeFacts.IsInterface(baseType))
        {
            return static (from, to) => new TypeLink.Inherits { From = from, To = to };
        }

        if (TypeFacts.IsInterface(type))
        {
            return static (from, to) => new TypeLink.Inherits { From = from, To = to };
        }

        return static (from, to) => new TypeLink.Implements { From = from, To = to };
    }

    private static void AddHeld(List<Relation> relations, ITypeDefinition type)
    {
        foreach (var property in type.Properties)
        {
            if (!IsShown(property))
            {
                continue;
            }

            var held = FindHeld(property);
            if (held is not null)
            {
                relations.Add(held.Value);
            }
        }
    }

    private static Relation? FindHeld(IProperty property)
    {
        if (TypeFacts.TryGetElementType(property, out var elementType))
        {
            return HeldRelation(elementType, (from, to) => new TypeLink.HoldsMany { From = from, To = to, Property = property.Name });
        }

        var type = property.ReturnType;
        if (TypeFacts.TryGetNullable(type, out var underlying))
        {
            type = underlying;
        }

        return HeldRelation(type, (from, to) => new TypeLink.HoldsOne { From = from, To = to, Property = property.Name });
    }

    private static Relation? HeldRelation(IType type, Func<CodeReference.Entity, CodeReference.Entity, TypeLink> toLink)
    {
        var definition = type.GetDefinition();
        if (definition is null)
        {
            return null;
        }

        if (definition.KnownTypeCode != KnownTypeCode.None)
        {
            return null;
        }

        return new Relation { Target = definition, ToLink = toLink };
    }

    private static List<ITypeDefinition> ListNeighbours(ITypeDefinition type)
    {
        var linked = ListRelations(type)
            .Select(static r => r.Target);
        var module = type.ParentModule;
        return linked
            .Concat(DerivedTypes.FindInModule(type))
            .Where(t => t.ParentModule == module)
            .Where(t => !t.Equals(type))
            .Where(IsShown)
            .Distinct()
            .ToList();
    }

    // ToLink is a function as the link is made only once both types' references are found.
    private readonly record struct Relation
    {
        public required ITypeDefinition Target { get; init; }

        public required Func<CodeReference.Entity, CodeReference.Entity, TypeLink> ToLink { get; init; }
    }
}
