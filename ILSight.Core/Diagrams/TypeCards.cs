using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Diagrams;

/// <summary>Types read as diagram cards from the type system, alone, with the types of their assembly
/// they are related to, or as the types deriving from a type. Each read loads the assemblies it needs
/// without listing them: call them off the UI thread. What the library throws as a read resolves the type or reads
/// its cards is that read's <see cref="Outcome{T}.Failed"/>; the cancellation of the token it was given is let
/// through. Inside a read's catch is only what touches the library's objects: the types walked to and what each card
/// is made of (<see cref="ReadType"/>); the app's rules over those values, the members' natural order, the hidden
/// members left out and the shapes, run after it, so a bug of the app's is never said as an unreadable type.</summary>
public static class TypeCards
{
    /// <summary>The card of the type <paramref name="type"/> points at; not found when it points at no type,
    /// or that cannot be resolved; unreadable when the library could not read the type system of an assembly
    /// resolving it reads.</summary>
    public static async Task<Outcome<Lookup<TypeCard>>> ReadAsync(AssemblyList list, CodeReference.Entity type, CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        Lookup<ReadType> read;
        try
        {
            var definition = await AssemblyListing.FindTypeDefinitionAsync(list, reading, type, ct);
            read = ReadFound(definition);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<TypeCard>>.Failed(e);
        }

        return new Outcome<Lookup<TypeCard>>.Ok(MakeFound(read));
    }

    /// <summary>The card of the type <paramref name="type"/> points at, then the cards of the types of its
    /// own assembly it is directly related to: those it derives from, implements or holds, and those
    /// deriving from it or implementing it. None when it points at no type; the module, with why, when the library
    /// could not read the type system of an assembly resolving it reads.</summary>
    public static async Task<Outcome<TypeSystemWork<List<TypeCard>>>> ReadWithNeighboursAsync(AssemblyList list, CodeReference.Entity type,
        CancellationToken cancellationToken)
    {
        using var reading = LoadedAssemblies.BeginReading();
        TypeSystemWork<List<ReadType>> read;
        try
        {
            var definition = await AssemblyListing.FindTypeDefinitionAsync(list, reading, type, cancellationToken);
            read = definition.Match<TypeSystemWork<List<ReadType>>>(
                found => new TypeSystemWork<List<ReadType>>.Done { Value = ReadWithNeighbours(found.Value, cancellationToken) },
                static _ => new TypeSystemWork<List<ReadType>>.Done { Value = [] },
                static unreadable => new TypeSystemWork<List<ReadType>>.Unreadable { Module = unreadable.Module });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<TypeSystemWork<List<TypeCard>>>.Failed(e);
        }

        var cards = read.Match<TypeSystemWork<List<TypeCard>>>(
            static done => new TypeSystemWork<List<TypeCard>>.Done { Value = done.Value.Select(Make).ToList() },
            static unreadable => new TypeSystemWork<List<TypeCard>>.Unreadable { Module = unreadable.Module },
            static closed => new TypeSystemWork<List<TypeCard>>.Closed { Module = closed.Module });
        return new Outcome<TypeSystemWork<List<TypeCard>>>.Ok(cards);
    }

    // The type first, then its neighbours; none when the type itself has no card.
    private static List<ReadType> ReadWithNeighbours(ITypeDefinition definition, CancellationToken cancellationToken)
    {
        var read = Read(definition);
        if (read is null)
        {
            return [];
        }

        var reads = new List<ReadType> { read };
        foreach (var neighbour in ListNeighbours(definition))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var neighbourRead = Read(neighbour);
            if (neighbourRead is not null)
            {
                reads.Add(neighbourRead);
            }
        }
        return reads;
    }

    /// <summary>The card of the type <paramref name="anchor"/> names; not found when its assembly cannot be
    /// read or no longer defines it; unreadable when the library could not read the assembly's type system.</summary>
    public static async Task<Outcome<Lookup<TypeCard>>> ReadAsync(AssemblyList list, TypeAnchor anchor, CancellationToken ct)
    {
        using var reading = LoadedAssemblies.BeginReading();
        Lookup<ReadType> read;
        try
        {
            var definition = await AssemblyListing.FindTypeDefinitionAsync(list, reading, anchor, ct);
            read = ReadFound(definition);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<Lookup<TypeCard>>.Failed(e);
        }

        return new Outcome<Lookup<TypeCard>>.Ok(MakeFound(read));
    }

    private static Lookup<ReadType> ReadFound(Lookup<ITypeDefinition> definition)
    {
        return definition.Match(
            static found => Lookup.FromNullable(Read(found.Value)),
            static _ => new Lookup<ReadType>.NotFound(),
            static unreadable => new Lookup<ReadType>.Unreadable { Module = unreadable.Module });
    }

    private static Lookup<TypeCard> MakeFound(Lookup<ReadType> read)
    {
        return read.Match<Lookup<TypeCard>>(
            static found => new Lookup<TypeCard>.Found { Value = Make(found.Value) },
            static _ => new Lookup<TypeCard>.NotFound(),
            static unreadable => new Lookup<TypeCard>.Unreadable { Module = unreadable.Module });
    }

    /// <summary>The cards of the types deriving directly from, or implementing, the type
    /// <paramref name="type"/> points at, among the assemblies <paramref name="list"/> holds now, once the reads of
    /// the type systems the search can reach ended, beside each module the search reached that the library could not
    /// read; none when it points at no type; no cards and its module when the library could not read the type system
    /// of an assembly resolving it reads.</summary>
    public static async Task<Outcome<ScopeSearch<TypeCard>>> ReadDerivedAsync(AssemblyList list, CodeReference.Entity type,
        CancellationToken cancellationToken)
    {
        using var reading = LoadedAssemblies.BeginReading();
        Lookup<ITypeDefinition> definition;
        try
        {
            definition = await AssemblyListing.FindTypeDefinitionAsync(list, reading, type, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<ScopeSearch<TypeCard>>.Failed(e);
        }

        return await definition.Match(
            found => ReadDerivedAsync(list, reading, found.Value, cancellationToken),
            static _ => Task.FromResult<Outcome<ScopeSearch<TypeCard>>>(new Outcome<ScopeSearch<TypeCard>>.Ok(new ScopeSearch<TypeCard> { Found = [], Unreadable = [] })),
            static unreadable => Task.FromResult<Outcome<ScopeSearch<TypeCard>>>(
                new Outcome<ScopeSearch<TypeCard>>.Ok(new ScopeSearch<TypeCard> { Found = [], Unreadable = [unreadable.Module] })));
    }

    private static async Task<Outcome<ScopeSearch<TypeCard>>> ReadDerivedAsync(AssemblyList list, ModuleReading reading, ITypeDefinition definition,
        CancellationToken cancellationToken)
    {
        var module = AssemblyListing.GetLoadedFrom(list, DecompilerFacts.GetMetadataFile(definition));
        var reachable = await DerivedTypes.ReadReachableAsync(reading, module, cancellationToken);
        ScopeSearch<ITypeDefinition> search;
        List<ReadType> reads;
        try
        {
            search = DerivedTypes.Search(list, reachable, definition, cancellationToken);
            reads = search.Found
                .Where(IsShown)
                .Select(Read)
                .OfType<ReadType>()
                .ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<ScopeSearch<TypeCard>>.Failed(e);
        }

        var cards = reads.Select(Make).ToList();
        return new Outcome<ScopeSearch<TypeCard>>.Ok(new ScopeSearch<TypeCard> { Found = cards, Unreadable = search.Unreadable });
    }

    // What the card of the type is made of, read from the library; null when it has no metadata file or token to be
    // referenced by, as a type the decompiler made up has not.
    private static ReadType? Read(ITypeDefinition type)
    {
        var reference = CodeReferences.Find(type);
        if (reference is null)
        {
            return null;
        }

        return new ReadType
        {
            Reference = reference,
            ReflectionName = type.ReflectionName,
            Label = EntityText.Label(type),
            FullName = EntityText.FullName(type),
            Kind = type.Kind,
            IsRecord = type.IsRecord,
            IsInterface = TypeFacts.IsInterface(type),
            IsAbstract = type.IsAbstract,
            IsSealed = type.IsSealed,
            Accessibility = type.Accessibility,
            Fields = ReadMembers(type.Fields),
            Properties = ReadMembers(type.Properties),
            Methods = ReadMembers(type.Methods),
            Events = ReadMembers(type.Events),
            Ancestors = ReadAncestors(type),
            Relations = ReadRelations(type),
            CanHaveDerived = DerivedTypes.CanHave(type),
        };
    }

    private static List<ReadMember> ReadMembers(IEnumerable<IMember> members)
    {
        return members
            .Select(ReadMemberOf)
            .OfType<ReadMember>()
            .ToList();
    }

    // Null when the member has no metadata file or token to be referenced by.
    private static ReadMember? ReadMemberOf(IMember member)
    {
        var reference = CodeReferences.Find(member);
        if (reference is null)
        {
            return null;
        }

        var method = member as IMethod;
        return new ReadMember
        {
            Reference = reference,
            Name = member.Name,
            Label = EntityText.Label(member),
            Kind = member.SymbolKind,
            IsConst = member is IField { IsConst: true },
            Accessibility = member.Accessibility,
            IsHidden = Members.IsHidden(member),
            IsStatic = member.IsStatic,
            IsMethod = method is not null,
            IsAccessor = method is not null && method.IsAccessor,
            OverridesObject = method is not null && TypeFacts.OverridesObject(method),
        };
    }

    // The library lists base types from object down; read in that order.
    private static List<ReadAncestor> ReadAncestors(ITypeDefinition type)
    {
        return type.GetNonInterfaceBaseTypes()
            .Where(t => TypeFacts.IsAncestor(type, t))
            .Select(ReadAncestorOf)
            .ToList();
    }

    private static ReadAncestor ReadAncestorOf(IType ancestor)
    {
        var definition = ancestor.GetDefinition();
        if (definition is null)
        {
            return new ReadAncestor { From = EntityText.ShortTypeName(ancestor), AssemblyPath = null, Members = [] };
        }

        return new ReadAncestor
        {
            From = EntityText.ShortTypeName(ancestor),
            AssemblyPath = CodeReferences.Find(definition)?.FilePath,
            Members = ReadMembers(definition.Fields.Concat<IMember>(definition.Properties).Concat(definition.Methods).Concat(definition.Events)),
        };
    }

    private static List<ReadRelation> ReadRelations(ITypeDefinition type)
    {
        return ListRelations(type)
            .Select(static r => new ReadRelation { To = CodeReferences.Find(r.Target), ToLink = r.ToLink })
            .ToList();
    }

    private static TypeCard Make(ReadType type)
    {
        return new TypeCard
        {
            Type = type.Reference,
            Anchor = new TypeAnchor { AssemblyPath = type.Reference.FilePath, FullName = type.ReflectionName },
            Name = type.Label,
            FullName = type.FullName,
            AssemblyPath = type.Reference.FilePath,
            Shape = EntityText.ShapeOf(type.Kind, type.IsRecord),
            Accessibility = EntityText.Map(type.Accessibility),
            Stereotype = StereotypeOf(type),
            Sections = MakeSections(type),
            Inherited = MakeInherited(type),
            Links = MakeLinks(type),
            CanHaveDerived = type.CanHaveDerived,
        };
    }

    private static TypeStereotype StereotypeOf(ReadType type)
    {
        if (type.IsInterface)
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

    private static List<CardSection> MakeSections(ReadType type)
    {
        var sections = new List<CardSection>();
        AddSection(sections, MemberSection.Fields, ListFields(type));
        AddSection(sections, MemberSection.Properties, Sort(type.Properties));
        AddSection(sections, MemberSection.Methods, Sort(ListMethods(type.Methods)));
        AddSection(sections, MemberSection.Events, Sort(type.Events));
        return sections;
    }

    private static void AddSection(List<CardSection> sections, MemberSection kind, IEnumerable<ReadMember> members)
    {
        var shown = members
            .Where(static m => !m.IsHidden)
            .Select(ToCardMember)
            .ToList();
        if (shown.Count == 0)
        {
            return;
        }

        sections.Add(new CardSection { Kind = kind, Members = shown });
    }

    // As the tree lists them: an enum's values in the order declared, without its one instance field, which holds the
    // value.
    private static IEnumerable<ReadMember> ListFields(ReadType type)
    {
        if (type.Kind != TypeKind.Enum)
        {
            return Sort(type.Fields);
        }

        return type.Fields.Where(static f => f.IsStatic);
    }

    private static IEnumerable<ReadMember> ListMethods(IEnumerable<ReadMember> methods)
    {
        return methods.Where(static m => !m.IsAccessor);
    }

    private static IEnumerable<ReadMember> Sort(IEnumerable<ReadMember> members)
    {
        return members.OrderBy(static m => m.Name, NaturalStringComparer.Instance);
    }

    private static bool IsShown(IEntity entity)
    {
        return !Members.IsHidden(entity);
    }

    private static CardMember ToCardMember(ReadMember member)
    {
        return new CardMember
        {
            Reference = member.Reference,
            Text = member.Label,
            Shape = EntityText.ShapeOf(member.Kind, member.IsConst),
            Accessibility = EntityText.Map(member.Accessibility),
        };
    }

    // Nearest first: the library lists base types from object down. For an interface, they are the interfaces it
    // extends.
    private static List<InheritedGroup> MakeInherited(ReadType type)
    {
        return Enumerable.Reverse(type.Ancestors)
            .Select(MakeInheritedFrom)
            .Where(static g => g.Members.Count > 0)
            .ToList();
    }

    private static InheritedGroup MakeInheritedFrom(ReadAncestor ancestor)
    {
        var members = ancestor.Members
            .Where(static m => !m.IsAccessor)
            .Where(IsInherited)
            .OrderBy(static m => m.Name, NaturalStringComparer.Instance)
            .Select(ToCardMember)
            .ToList();
        return new InheritedGroup { From = ancestor.From, AssemblyPath = ancestor.AssemblyPath, Members = members };
    }

    private static bool IsInherited(ReadMember member)
    {
        if (member.IsHidden)
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

        if (member.IsMethod)
        {
            return IsInheritedMethod(member);
        }

        return true;
    }

    private static bool IsInheritedMethod(ReadMember method)
    {
        if (method.Kind == SymbolKind.Constructor)
        {
            return false;
        }

        return !method.OverridesObject;
    }

    private static List<TypeLink> MakeLinks(ReadType type)
    {
        return type.Relations
            .Select(r => LinkOf(type.Reference, r))
            .OfType<TypeLink>()
            .Distinct()
            .ToList();
    }

    private static TypeLink? LinkOf(CodeReference.Entity from, ReadRelation relation)
    {
        var to = relation.To;
        if (to is null)
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
            .Concat(DerivedTypes.FindDirectInOwnModule(type))
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

    // A relation walked, its target's reference read: null when it has none to be referenced by.
    private sealed record ReadRelation
    {
        public required CodeReference.Entity? To { get; init; }

        public required Func<CodeReference.Entity, CodeReference.Entity, TypeLink> ToLink { get; init; }
    }

    // What a card is made of, read from the library's type: plain values, the type's objects left behind.
    private sealed record ReadType
    {
        public required CodeReference.Entity Reference { get; init; }

        public required string ReflectionName { get; init; }

        public required string Label { get; init; }

        public required string FullName { get; init; }

        public required TypeKind Kind { get; init; }

        public required bool IsRecord { get; init; }

        public required bool IsInterface { get; init; }

        public required bool IsAbstract { get; init; }

        public required bool IsSealed { get; init; }

        public required Accessibility Accessibility { get; init; }

        // Each in the order the library declares them.
        public required List<ReadMember> Fields { get; init; }

        public required List<ReadMember> Properties { get; init; }

        public required List<ReadMember> Methods { get; init; }

        public required List<ReadMember> Events { get; init; }

        // From object down, as the library lists them.
        public required List<ReadAncestor> Ancestors { get; init; }

        public required List<ReadRelation> Relations { get; init; }

        public required bool CanHaveDerived { get; init; }
    }

    // A member as read for a card: what the card shows of it and the facts the app's rules ask of it.
    private sealed record ReadMember
    {
        public required CodeReference.Entity Reference { get; init; }

        public required string Name { get; init; }

        public required string Label { get; init; }

        public required SymbolKind Kind { get; init; }

        public required bool IsConst { get; init; }

        public required Accessibility Accessibility { get; init; }

        // As the library says: no metadata, or made up by the compiler.
        public required bool IsHidden { get; init; }

        public required bool IsStatic { get; init; }

        public required bool IsMethod { get; init; }

        public required bool IsAccessor { get; init; }

        public required bool OverridesObject { get; init; }
    }

    private sealed record ReadAncestor
    {
        public required string From { get; init; }

        public required AssemblyPath? AssemblyPath { get; init; }

        // Its fields, properties, methods and events, in that order.
        public required List<ReadMember> Members { get; init; }
    }
}
