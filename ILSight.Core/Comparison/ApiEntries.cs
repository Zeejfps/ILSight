using System.Diagnostics;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Comparison;

/// <summary>The entries of a comparison, built by matching each side's namespaces, types and members by
/// <see cref="ApiKeys"/> and telling them apart by <see cref="ApiDeclarations"/>.</summary>
public static class ApiEntries
{
    private const string GlobalNamespaceText = "-";
    private const string ModuleTypeName = "<Module>";

    private static readonly Dictionary<string, Compared> None = new();

    /// <summary>The namespaces either module puts types in that <paramref name="scope"/> takes in, sorted by name.</summary>
    public static List<ApiEntry> CompareNamespaces(MetadataModule left, MetadataModule right, ApiScope scope, CancellationToken cancellationToken)
    {
        var leftTypes = ListTypesByNamespace(left, scope);
        var rightTypes = ListTypesByNamespace(right, scope);
        var names = ListPresence(leftTypes.Keys, rightTypes.Keys)
            .OrderBy(static n => n.Name, NaturalStringComparer.Instance)
            .ToList();
        var entries = new List<ApiEntry>();
        foreach (var (name, presence) in names)
        {
            var leftOnes = leftTypes.GetValueOrDefault(name) ?? None;
            var rightOnes = rightTypes.GetValueOrDefault(name) ?? None;
            var children = CompareAll(leftOnes, rightOnes, scope, cancellationToken);
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

    private static Dictionary<string, Dictionary<string, Compared>> ListTypesByNamespace(MetadataModule module, ApiScope scope)
    {
        var byNamespace = new Dictionary<string, Dictionary<string, Compared>>(StringComparer.Ordinal);
        foreach (var type in module.TopLevelTypeDefinitions)
        {
            if (IsModuleType(type))
            {
                continue;
            }

            if (!IsTakenIn(type, scope))
            {
                continue;
            }

            var types = FindOrAddNamespace(byNamespace, type.Namespace);
            AddByKey(types, type);
        }
        return byNamespace;
    }

    // Numbered when others took the key before it, so none is dropped; those that share a key are matched in the order
    // the metadata lists them.
    private static void AddByKey(Dictionary<string, Compared> entities, IEntity entity)
    {
        var reference = CodeReferences.Find(entity);
        if (reference == null)
        {
            return;
        }

        var compared = new Compared { Entity = entity, Reference = reference };
        AddUnderFreeKey(entities, ApiKeys.Of(entity), compared);
    }

    private static void AddUnderFreeKey(Dictionary<string, Compared> entities, string key, Compared compared)
    {
        var candidate = key;
        var number = 2;
        while (!entities.TryAdd(candidate, compared))
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

    private static Dictionary<string, Compared> FindOrAddNamespace(Dictionary<string, Dictionary<string, Compared>> byNamespace, string name)
    {
        if (byNamespace.TryGetValue(name, out var types))
        {
            return types;
        }

        types = new Dictionary<string, Compared>(StringComparer.Ordinal);
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

    private static List<ApiEntry> CompareAll(Dictionary<string, Compared> left, Dictionary<string, Compared> right, ApiScope scope,
        CancellationToken cancellationToken)
    {
        var entries = new List<ApiEntry>(left.Count + right.Count);
        foreach (var (key, leftOne) in left)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (right.TryGetValue(key, out var rightOne))
            {
                entries.Add(CompareBoth(leftOne, rightOne, scope, cancellationToken));
            }
            else
            {
                entries.Add(Removed(leftOne, scope, cancellationToken));
            }
        }
        foreach (var (key, rightOne) in right)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!left.ContainsKey(key))
            {
                entries.Add(Added(rightOne, scope, cancellationToken));
            }
        }
        // Sorted as the tree sorts them.
        return entries
            .OrderBy(static e => SortGroup(e.Shape))
            .ThenBy(static e => e.Text, NaturalStringComparer.Instance)
            .ToList();
    }

    private static ApiEntry Removed(Compared left, ApiScope scope, CancellationToken cancellationToken)
    {
        var children = CompareAll(ListChildren(left, scope), None, scope, cancellationToken);
        return EntryOf(left, new ApiSides.LeftOnly(SideOf(left)), children);
    }

    private static ApiEntry Added(Compared right, ApiScope scope, CancellationToken cancellationToken)
    {
        var children = CompareAll(None, ListChildren(right, scope), scope, cancellationToken);
        return EntryOf(right, new ApiSides.RightOnly(SideOf(right)), children);
    }

    private static ApiEntry CompareBoth(Compared left, Compared right, ApiScope scope, CancellationToken cancellationToken)
    {
        var children = CompareAll(ListChildren(left, scope), ListChildren(right, scope), scope, cancellationToken);
        return EntryOf(right, new ApiSides.Both { Left = SideOf(left), Right = SideOf(right) }, children);
    }

    private static ApiEntry EntryOf(Compared compared, ApiSides sides, List<ApiEntry> children)
    {
        var shown = compared.Entity;
        var shape = EntityText.SymbolShapeOf(shown);
        var accessibility = EntityText.Map(shown.Accessibility);
        return new ApiEntry.Entity
        {
            Shape = shape,
            Text = EntityText.Label(shown),
            Accessibility = accessibility,
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
    /// thread. A unit reading both: the first released (closed) before, when one was, with nothing compared.</summary>
    public static async Task<TypeSystemWork<ApiDiff>> CompareAsync(LoadedModule left, LoadedModule right, ApiScope scope, CancellationToken cancellationToken)
    {
        using var reading = LoadedAssemblies.BeginReading();
        var reads = await LoadedAssemblies.ReadTypeSystemsAsync(reading, [left, right], cancellationToken);
        var closed = new[] { left, right }.FirstOrDefault(m => !reads.Modules.Contains(m));
        if (closed is not null)
        {
            return new TypeSystemWork<ApiDiff>.Closed { Module = closed };
        }

        if (reads.Unreadable.Count > 0)
        {
            return new TypeSystemWork<ApiDiff>.Unreadable { Module = reads.Unreadable[0] };
        }

        var diff = Compare(left, reads.Read[LoadedAssemblies.GetFile(left)].MainModule, right, reads.Read[LoadedAssemblies.GetFile(right)].MainModule, scope,
            cancellationToken);
        return new TypeSystemWork<ApiDiff>.Done { Value = diff };
    }

    private static ApiDiff Compare(LoadedModule left, MetadataModule leftMain, LoadedModule right, MetadataModule rightMain, ApiScope scope,
        CancellationToken cancellationToken)
    {
        var namespaces = CompareNamespaces(leftMain, rightMain, scope, cancellationToken);
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

    private static ApiSide SideOf(Compared compared)
    {
        return new ApiSide { Reference = compared.Reference, Declaration = ApiDeclarations.Of(compared.Entity) };
    }

    private static Dictionary<string, Compared> ListChildren(Compared compared, ApiScope scope)
    {
        if (compared.Entity is not ITypeDefinition type)
        {
            return None;
        }

        var children = new Dictionary<string, Compared>(StringComparer.Ordinal);
        foreach (var member in ListMembers(type))
        {
            if (IsTakenIn(member, scope))
            {
                AddByKey(children, member);
            }
        }
        return children;
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

    private readonly record struct Compared
    {
        public required IEntity Entity { get; init; }

        public required CodeReference.Entity Reference { get; init; }
    }
}
