using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.DiagramArea;

/// <summary>Types read as diagram cards from the type system, alone, with the types of their assembly
/// they are related to, or as the types deriving from a type. Each read loads the assemblies it needs
/// without listing them: call them off the UI thread. What the library throws as a read resolves the type or reads
/// its cards is that read's <see cref="Outcome{T}.Failed"/>; the cancellation of the token it was given is let
/// through. Inside a read's catch is only what touches the library's objects: the facts a walk's choice needs, the
/// types chosen and what each card is made of (<see cref="ReadType"/>); the app's rules over those values, which of the
/// types walked to are shown, which relations are links, the members' natural order, the hidden members left out and
/// the shapes, run after it, so a bug of the app's is never said as an unreadable type. A walk reads the facts its
/// choice needs under one catch, applies the app's rule outside, then reads what it chose under another.</summary>
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
        Lookup<Walk> walked;
        try
        {
            var definition = await AssemblyListing.FindTypeDefinitionAsync(list, reading, type, cancellationToken);
            walked = definition switch
            {
                Lookup<ITypeDefinition>.Found found => Lookup.FromNullable(WalkFrom(found.Value)),
                Lookup<ITypeDefinition>.NotFound => new Lookup<Walk>.NotFound(),
                Lookup<ITypeDefinition>.Unreadable unreadable => new Lookup<Walk>.Unreadable { Module = unreadable.Module },
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<TypeSystemWork<List<TypeCard>>>.Failed(e);
        }

        return walked switch
        {
            Lookup<Walk>.Found found => ReadNeighbours(found.Value, cancellationToken),
            Lookup<Walk>.NotFound => new Outcome<TypeSystemWork<List<TypeCard>>>.Ok(new TypeSystemWork<List<TypeCard>>.Done { Value = [] }),
            Lookup<Walk>.Unreadable unreadable => new Outcome<TypeSystemWork<List<TypeCard>>>.Ok(new TypeSystemWork<List<TypeCard>>.Unreadable { Module = unreadable.Module }),
        };
    }

    // The type's card read and the types it could be shown with, each with the facts the app's rule asks of it; null when
    // the type itself has no card.
    private static Walk? WalkFrom(ITypeDefinition definition)
    {
        var read = Read(definition);
        if (read is null)
        {
            return null;
        }

        return new Walk { Type = read, Neighbours = ListNeighbours(definition) };
    }

    // The app's rule over the facts walked, outside the read's catch, then the cards of the types it chose, read under
    // their own: the type first, then its neighbours.
    private static Outcome<TypeSystemWork<List<TypeCard>>> ReadNeighbours(Walk walk, CancellationToken cancellationToken)
    {
        var shown = walk.Neighbours.Where(IsShownNeighbour).ToList();
        var reads = new List<ReadType> { walk.Type };
        try
        {
            foreach (var neighbour in shown)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = Read(neighbour.Type);
                if (read is not null)
                {
                    reads.Add(read);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<TypeSystemWork<List<TypeCard>>>.Failed(e);
        }

        return new Outcome<TypeSystemWork<List<TypeCard>>>.Ok(new TypeSystemWork<List<TypeCard>>.Done { Value = reads.Select(Make).ToList() });
    }

    // The app's rule: a neighbour is shown when it is linked to the type or derives from it, is of the type's own module,
    // is not the type itself, and is not hidden.
    private static bool IsShownNeighbour(Neighbour neighbour)
    {
        var related = neighbour.IsDerived || (neighbour.IsBase && IsLink(true, neighbour.Code)) || (neighbour.IsHeld && IsLink(false, neighbour.Code));
        return related && neighbour.IsInOwnModule && !neighbour.IsSelf && !neighbour.IsHidden;
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
        return definition switch
        {
            Lookup<ITypeDefinition>.Found found => Lookup.FromNullable(Read(found.Value)),
            Lookup<ITypeDefinition>.NotFound => new Lookup<ReadType>.NotFound(),
            Lookup<ITypeDefinition>.Unreadable unreadable => new Lookup<ReadType>.Unreadable { Module = unreadable.Module },
        };
    }

    private static Lookup<TypeCard> MakeFound(Lookup<ReadType> read)
    {
        return read.Select(Make);
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

        return definition switch
        {
            Lookup<ITypeDefinition>.Found found => await ReadDerivedAsync(list, reading, found.Value, cancellationToken),
            Lookup<ITypeDefinition>.NotFound => new Outcome<ScopeSearch<TypeCard>>.Ok(new ScopeSearch<TypeCard> { Found = [], Unreadable = [] }),
            Lookup<ITypeDefinition>.Unreadable unreadable => new Outcome<ScopeSearch<TypeCard>>.Ok(new ScopeSearch<TypeCard> { Found = [], Unreadable = [unreadable.Module] }),
        };
    }

    private static async Task<Outcome<ScopeSearch<TypeCard>>> ReadDerivedAsync(AssemblyList list, ModuleReadLease reading, ITypeDefinition definition,
        CancellationToken cancellationToken)
    {
        var module = AssemblyListing.GetLoadedFrom(list, DecompilerFacts.GetMetadataFile(definition));
        var reachable = await DerivedTypes.ReadReachableAsync(reading, module, cancellationToken);
        ScopeSearch<ITypeDefinition> search;
        List<(ITypeDefinition Type, bool IsHidden)> found;
        try
        {
            search = DerivedTypes.Search(list, reachable, definition, cancellationToken);
            found = search.Found.Select(static t => (t, Members.IsHidden(t))).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<ScopeSearch<TypeCard>>.Failed(e);
        }

        // The app's rule, outside the read's catch: a hidden type has no card. Then the cards of those shown.
        var shown = found.Where(static t => !t.IsHidden).Select(static t => t.Type).ToList();
        List<ReadType> reads;
        try
        {
            reads = shown.Select(Read).OfType<ReadType>().ToList();
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

    // The library lists base types from object down, then the type itself, which is no ancestor; read in that order.
    // Object's are read too: which ancestors' members are shown is the app's rule, decided outside (MakeInherited).
    private static List<ReadAncestor> ReadAncestors(ITypeDefinition type)
    {
        return type.GetNonInterfaceBaseTypes()
            .Where(t => !t.Equals(type))
            .Select(ReadAncestorOf)
            .ToList();
    }

    private static ReadAncestor ReadAncestorOf(IType ancestor)
    {
        var isObject = TypeFacts.IsObject(ancestor);
        var definition = ancestor.GetDefinition();
        if (definition is null)
        {
            return new ReadAncestor { From = EntityText.ShortTypeName(ancestor), IsObject = isObject, AssemblyPath = null, Members = [] };
        }

        return new ReadAncestor
        {
            From = EntityText.ShortTypeName(ancestor),
            IsObject = isObject,
            AssemblyPath = CodeReferences.Find(definition)?.FilePath,
            Members = ReadMembers(definition.Fields.Concat<IMember>(definition.Properties).Concat(definition.Methods).Concat(definition.Events)),
        };
    }

    private static List<ReadRelation> ReadRelations(ITypeDefinition type)
    {
        return ListRelations(type)
            .Select(static r => new ReadRelation { To = CodeReferences.Find(r.Target), ToLink = r.ToLink, IsBase = r.IsBase, TargetCode = r.Target.KnownTypeCode })
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
    // extends. The app's rule: object's members are not shown as inherited.
    private static List<InheritedGroup> MakeInherited(ReadType type)
    {
        return Enumerable.Reverse(type.Ancestors)
            .Where(static a => !a.IsObject)
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
        var links = new List<TypeLink>();
        var seen = new HashSet<TypeLink>();
        foreach (var relation in type.Relations)
        {
            if (!IsLink(relation.IsBase, relation.TargetCode))
            {
                continue;
            }

            var to = relation.To;
            if (to is null)
            {
                continue;
            }

            var link = relation.ToLink(type.Reference, to);
            if (seen.Add(link))
            {
                links.Add(link);
            }
        }
        return links;
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

            relations.Add(new Relation { Target = definition, ToLink = BaseLinkOf(type, definition), IsBase = true });
        }
    }

    // The app's rule over a relation's facts: a base every type of its kind has is no link, nor is holding a type the
    // runtime knows (a string, a number).
    private static bool IsLink(bool isBase, KnownTypeCode target)
    {
        if (isBase)
        {
            return target is not (KnownTypeCode.Object or KnownTypeCode.ValueType or KnownTypeCode.Enum);
        }

        return target == KnownTypeCode.None;
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

        return new Relation { Target = definition, ToLink = toLink, IsBase = false };
    }

    // The types the type is linked to and those deriving from it in its own module, each once, with the facts the app's
    // rule asks of them (IsShownNeighbour).
    private static List<Neighbour> ListNeighbours(ITypeDefinition type)
    {
        var relations = ListRelations(type);
        var derived = DerivedTypes.FindDirectInOwnModule(type).ToList();
        var module = type.ParentModule;
        return relations
            .Select(static r => r.Target)
            .Concat(derived)
            .Distinct()
            .Select(t => new Neighbour
            {
                Type = t,
                Code = t.KnownTypeCode,
                IsBase = relations.Exists(r => r.IsBase && r.Target.Equals(t)),
                IsHeld = relations.Exists(r => !r.IsBase && r.Target.Equals(t)),
                IsDerived = derived.Contains(t),
                IsInOwnModule = t.ParentModule == module,
                IsSelf = t.Equals(type),
                IsHidden = Members.IsHidden(t),
            })
            .ToList();
    }

    // ToLink is a function as the link is made only once both types' references are found. A base, else a type held.
    private readonly record struct Relation
    {
        public required ITypeDefinition Target { get; init; }

        public required Func<CodeReference.Entity, CodeReference.Entity, TypeLink> ToLink { get; init; }

        public required bool IsBase { get; init; }
    }

    // A relation walked, its target's reference read: null when it has none to be referenced by.
    private sealed record ReadRelation
    {
        public required CodeReference.Entity? To { get; init; }

        public required Func<CodeReference.Entity, CodeReference.Entity, TypeLink> ToLink { get; init; }

        public required bool IsBase { get; init; }

        public required KnownTypeCode TargetCode { get; init; }
    }

    // A type and the types it could be shown with, read.
    private sealed record Walk
    {
        public required ReadType Type { get; init; }

        public required List<Neighbour> Neighbours { get; init; }
    }

    // A type a walk could show beside the one it began from, with the facts the app's rule asks of it, read from the
    // library.
    private sealed record Neighbour
    {
        public required ITypeDefinition Type { get; init; }

        public required KnownTypeCode Code { get; init; }

        // How the type is related: one of its bases, held by one of its properties, deriving from it.
        public required bool IsBase { get; init; }

        public required bool IsHeld { get; init; }

        public required bool IsDerived { get; init; }

        public required bool IsInOwnModule { get; init; }

        public required bool IsSelf { get; init; }

        public required bool IsHidden { get; init; }
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

        public required bool IsObject { get; init; }

        public required AssemblyPath? AssemblyPath { get; init; }

        // Its fields, properties, methods and events, in that order.
        public required List<ReadMember> Members { get; init; }
    }
}
