using System.Diagnostics;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Comparison;

/// <summary>The entries of a comparison, built by matching each side's namespaces, types and members by
/// <see cref="ApiKeys"/> and telling them apart by <see cref="ApiDeclarations"/>. Each side's API is read out of the
/// library's objects first, under its own catch (<c>ReadApi</c>); the matching, ordering and counting are done after,
/// on what was read.</summary>
public static class ApiEntries
{
    private const string GlobalNamespaceText = "-";
    private const string ModuleTypeName = "<Module>";

    private static readonly Dictionary<string, ApiFacts> None = new();

    // The namespaces either module's API puts types in, sorted by name: the app's matching, on what each read took out.
    private static List<ApiEntry> CompareNamespaces(ModuleApi left, ModuleApi right, CancellationToken cancellationToken)
    {
        var names = ListPresence(left.TypesByNamespace.Keys, right.TypesByNamespace.Keys)
            .OrderBy(static n => n.Name, NaturalStringComparer.Instance)
            .ToList();
        var entries = new List<ApiEntry>();
        foreach (var (name, presence) in names)
        {
            var leftOnes = KeyOf(left.TypesByNamespace.GetValueOrDefault(name));
            var rightOnes = KeyOf(right.TypesByNamespace.GetValueOrDefault(name));
            var children = CompareAll(leftOnes, rightOnes, cancellationToken);
            entries.Add(CreateNamespace(NamespaceText(name), presence, children));
        }
        return entries;
    }

    private static List<(string Name, ApiPresence Presence)> ListPresence(IReadOnlyCollection<string> left, IReadOnlyCollection<string> right)
    {
        var rightSet = right.ToHashSet(StringComparer.Ordinal);
        var leftSet = left.ToHashSet(StringComparer.Ordinal);
        var names = new List<(string, ApiPresence)>();
        foreach (var name in left)
        {
            names.Add((name, rightSet.Contains(name) ? ApiPresence.Both : ApiPresence.LeftOnly));
        }

        foreach (var name in right)
        {
            if (!leftSet.Contains(name))
            {
                names.Add((name, ApiPresence.RightOnly));
            }
        }
        return names;
    }

    // The library's read of one module's API, under its own catch: the types scope takes in, by namespace, each with
    // what is shown of it and the members it holds, read out into the app's values; what the library throws reading
    // them, the comparison's failure. Nothing is matched, ordered or counted here: that is the comparison's, after it.
    private static Outcome<ModuleApi> ReadApi(MetadataModule module, ApiScope scope, CancellationToken cancellationToken)
    {
        try
        {
            var types = ReadTypesByNamespace(module.TopLevelTypeDefinitions, scope, cancellationToken);
            return new Outcome<ModuleApi>.Ok(new ModuleApi { TypesByNamespace = types });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return new Outcome<ModuleApi>.Failed(e);
        }
    }

    private static Dictionary<string, List<ApiFacts>> ReadTypesByNamespace(IEnumerable<ITypeDefinition> types, ApiScope scope,
        CancellationToken cancellationToken)
    {
        var byNamespace = new Dictionary<string, List<ApiFacts>>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsModuleType(type))
            {
                continue;
            }

            if (!IsTakenIn(type, scope))
            {
                continue;
            }

            var facts = ReadFacts(type, scope, cancellationToken);
            if (facts is null)
            {
                continue;
            }

            var namespaceTypes = FindOrAddNamespace(byNamespace, type.Namespace);
            namespaceTypes.Add(facts);
        }
        return byNamespace;
    }

    // What the comparison needs of entity, read whole; null for one no reference can point at, which isn't compared.
    private static ApiFacts? ReadFacts(IEntity entity, ApiScope scope, CancellationToken cancellationToken)
    {
        var reference = CodeReferences.Find(entity);
        if (reference == null)
        {
            return null;
        }

        return new ApiFacts
        {
            Key = ApiKeys.Of(entity),
            Reference = reference,
            Declaration = ApiDeclarations.Of(entity),
            Shape = EntityText.SymbolShapeOf(entity),
            Text = EntityText.Label(entity),
            Accessibility = entity.Accessibility,
            Children = ReadChildren(entity, scope, cancellationToken),
        };
    }

    private static List<ApiFacts> ReadChildren(IEntity entity, ApiScope scope, CancellationToken cancellationToken)
    {
        var children = new List<ApiFacts>();
        if (entity is not ITypeDefinition type)
        {
            return children;
        }

        foreach (var member in ListMembers(type))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsTakenIn(member, scope))
            {
                continue;
            }

            var facts = ReadFacts(member, scope, cancellationToken);
            if (facts is not null)
            {
                children.Add(facts);
            }
        }
        return children;
    }

    // Keyed for matching, numbered when others took the key before it, so none is dropped; those that share a key are
    // matched in the order the metadata lists them.
    private static Dictionary<string, ApiFacts> KeyOf(List<ApiFacts>? read)
    {
        if (read is null)
        {
            return None;
        }

        var keyed = new Dictionary<string, ApiFacts>(StringComparer.Ordinal);
        foreach (var facts in read)
        {
            AddUnderFreeKey(keyed, facts.Key, facts);
        }
        return keyed;
    }

    private static void AddUnderFreeKey(Dictionary<string, ApiFacts> entities, string key, ApiFacts facts)
    {
        var candidate = key;
        var number = 2;
        while (!entities.TryAdd(candidate, facts))
        {
            candidate = $"{key}#{number}";
            number++;
        }
    }

    // Every module has a <Module> type to hold its global members, which C# never declares.
    private static bool IsModuleType(ITypeDefinition type)
    {
        if (type.Namespace.Length > 0)
        {
            return false;
        }

        return type.Name == ModuleTypeName;
    }

    private static bool IsTakenIn(IEntity entity, ApiScope scope)
    {
        if (Members.IsHidden(entity))
        {
            return false;
        }

        if (scope == ApiScope.AllDeclared)
        {
            return true;
        }

        return EntityText.IsPublicApi(entity);
    }

    private static List<ApiFacts> FindOrAddNamespace(Dictionary<string, List<ApiFacts>> byNamespace, string name)
    {
        if (byNamespace.TryGetValue(name, out var types))
        {
            return types;
        }

        types = [];
        byNamespace.Add(name, types);
        return types;
    }

    private static string NamespaceText(string name)
    {
        if (name.Length == 0)
        {
            return GlobalNamespaceText;
        }

        return name;
    }

    private static List<ApiEntry> CompareAll(Dictionary<string, ApiFacts> left, Dictionary<string, ApiFacts> right, CancellationToken cancellationToken)
    {
        var entries = new List<ApiEntry>(left.Count + right.Count);
        foreach (var (key, leftOne) in left)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (right.TryGetValue(key, out var rightOne))
            {
                entries.Add(CompareBoth(leftOne, rightOne, cancellationToken));
            }
            else
            {
                entries.Add(Removed(leftOne, cancellationToken));
            }
        }
        foreach (var (key, rightOne) in right)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!left.ContainsKey(key))
            {
                entries.Add(Added(rightOne, cancellationToken));
            }
        }
        // Sorted as the tree sorts them.
        return entries
            .OrderBy(static e => SortGroup(e.Shape))
            .ThenBy(static e => e.Text, NaturalStringComparer.Instance)
            .ToList();
    }

    private static ApiEntry Removed(ApiFacts left, CancellationToken cancellationToken)
    {
        var children = CompareAll(KeyOf(left.Children), None, cancellationToken);
        return EntryOf(left, new ApiSides.LeftOnly(SideOf(left)), children);
    }

    private static ApiEntry Added(ApiFacts right, CancellationToken cancellationToken)
    {
        var children = CompareAll(None, KeyOf(right.Children), cancellationToken);
        return EntryOf(right, new ApiSides.RightOnly(SideOf(right)), children);
    }

    private static ApiEntry CompareBoth(ApiFacts left, ApiFacts right, CancellationToken cancellationToken)
    {
        var children = CompareAll(KeyOf(left.Children), KeyOf(right.Children), cancellationToken);
        return EntryOf(right, new ApiSides.Both { Left = SideOf(left), Right = SideOf(right) }, children);
    }

    private static ApiEntry EntryOf(ApiFacts facts, ApiSides sides, List<ApiEntry> children)
    {
        return new ApiEntry.Entity
        {
            Shape = facts.Shape,
            Text = facts.Text,
            Accessibility = EntityText.Map(facts.Accessibility),
            Children = children,
            Sides = sides,
        };
    }

    private static ApiEntry.Namespace CreateNamespace(string text, ApiPresence presence, IReadOnlyList<ApiEntry> children)
    {
        return new ApiEntry.Namespace
        {
            Shape = new SymbolShape.Namespace(),
            Text = text,
            Accessibility = NodeAccessibility.None,
            Children = children,
            Presence = presence,
        };
    }

    /// <summary>Compares the API of <paramref name="left"/> with that of <paramref name="right"/>, taking in
    /// what <paramref name="scope"/> says, once the reads of both assemblies' type systems ended; the first the library
    /// could not read, when it could not, with nothing compared. Reads both assemblies' types: call it off the UI
    /// thread. A unit reading both: the first released (closed) before, when one was, with nothing compared. What the
    /// library throws reading their APIs is <see cref="Outcome{T}.Failed"/>, each side's read under its own catch; the
    /// comparison of what was read runs after both, so a bug of the app's goes on to the edge. The cancellation of
    /// <paramref name="cancellationToken"/> is let through.</summary>
    public static async Task<Outcome<TypeSystemWork<ApiDiff>>> CompareAsync(LoadedModule left, LoadedModule right, ApiScope scope,
        CancellationToken cancellationToken)
    {
        using var reading = LoadedAssemblies.BeginReading();
        var reads = await LoadedAssemblies.ReadTypeSystemsAsync(reading, [left, right], cancellationToken);
        var closed = new[] { left, right }.FirstOrDefault(m => !reads.Modules.Contains(m));
        if (closed is not null)
        {
            return new Outcome<TypeSystemWork<ApiDiff>>.Ok(new TypeSystemWork<ApiDiff>.Closed { Module = closed });
        }

        if (reads.Unreadable.Count > 0)
        {
            return new Outcome<TypeSystemWork<ApiDiff>>.Ok(new TypeSystemWork<ApiDiff>.Unreadable { Module = reads.Unreadable[0] });
        }

        var leftApi = ReadApi(reads.Read[LoadedAssemblies.GetFile(left)].MainModule, scope, cancellationToken);
        if (leftApi is Outcome<ModuleApi>.Failed leftFailed)
        {
            return new Outcome<TypeSystemWork<ApiDiff>>.Failed(leftFailed.Error);
        }

        var rightApi = ReadApi(reads.Read[LoadedAssemblies.GetFile(right)].MainModule, scope, cancellationToken);
        if (rightApi is Outcome<ModuleApi>.Failed rightFailed)
        {
            return new Outcome<TypeSystemWork<ApiDiff>>.Failed(rightFailed.Error);
        }

        // Matched, ordered and counted after both reads, outside their catches: a bug of the app's comparison goes on
        // to the edge, never said as the library's failure on either assembly.
        var namespaces = CompareNamespaces(((Outcome<ModuleApi>.Ok)leftApi).Value, ((Outcome<ModuleApi>.Ok)rightApi).Value, cancellationToken);
        return new Outcome<TypeSystemWork<ApiDiff>>.Ok(new TypeSystemWork<ApiDiff>.Done { Value = Compare(left, right, scope, namespaces) });
    }

    private static ApiDiff Compare(LoadedModule left, LoadedModule right, ApiScope scope, List<ApiEntry> namespaces)
    {
        return new ApiDiff
        {
            LeftName = left.NameAndVersion,
            RightName = right.NameAndVersion,
            Scope = scope,
            Namespaces = namespaces,
            Added = Count(namespaces, ApiChange.Added),
            Removed = Count(namespaces, ApiChange.Removed),
            Changed = Count(namespaces, ApiChange.Changed),
        };
    }

    private static ApiSide SideOf(ApiFacts facts)
    {
        return new ApiSide { Reference = facts.Reference, Declaration = facts.Declaration };
    }

    private static IEnumerable<IEntity> ListMembers(ITypeDefinition type)
    {
        var fields = type.Fields.Where(f => !IsEnumValueField(type, f));
        var methods = type.Methods.Where(static m => !m.IsAccessor);
        return type.NestedTypes
            .Concat<IEntity>(fields)
            .Concat(type.Properties)
            .Concat(type.Events)
            .Concat(methods);
    }

    private static bool IsEnumValueField(ITypeDefinition type, IField field)
    {
        if (type.Kind != TypeKind.Enum)
        {
            return false;
        }

        // An enum's members are static fields; its one instance field holds the value.
        return !field.IsStatic;
    }

    private static int SortGroup(SymbolShape shape)
    {
        return shape.Match(
            static _ => 0,
            static _ => 0,
            static _ => 0,
            static member => member.Shape switch
            {
                MemberShape.Field or MemberShape.Constant => 1,
                MemberShape.Property or MemberShape.Indexer => 2,
                MemberShape.Event => 3,
                MemberShape.Method or MemberShape.Constructor or MemberShape.Operator => 4,
                _ => throw new UnreachableException(),
            });
    }

    /// <summary>How many types and members under <paramref name="entries"/> changed as <paramref name="change"/>
    /// says, counting an added or removed one alone, not with what it holds.</summary>
    public static int Count(IReadOnlyList<ApiEntry> entries, ApiChange change)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            count += Count(entry, change);
        }

        return count;
    }

    private static int Count(ApiEntry entry, ApiChange change)
    {
        if (entry is not ApiEntry.Entity)
        {
            return Count(entry.Children, change);
        }

        var own = entry.Change == change ? 1 : 0;
        if (entry.Change is ApiChange.Added or ApiChange.Removed)
        {
            return own;
        }

        return own + Count(entry.Children, change);
    }

    // One module's API as its read took it out (ReadApi): the types taken in, by namespace, in the order the metadata
    // lists them.
    private sealed class ModuleApi
    {
        public required Dictionary<string, List<ApiFacts>> TypesByNamespace { get; init; }
    }

    // What the comparison needs of a type or member, read out of the library's objects: the key it is matched by, what
    // is shown of it, how it is declared, and the members it holds that the scope takes in.
    private sealed class ApiFacts
    {
        public required string Key { get; init; }

        public required CodeReference.Entity Reference { get; init; }

        public required string Declaration { get; init; }

        public required SymbolShape Shape { get; init; }

        public required string Text { get; init; }

        public required Accessibility Accessibility { get; init; }

        public required List<ApiFacts> Children { get; init; }
    }
}
