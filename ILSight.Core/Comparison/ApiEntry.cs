using System.Diagnostics;

namespace ILSight.Core.Comparison;

/// <summary>A namespace, type or member of either assembly compared, labelled as the tree labels it,
/// and what it holds, merged from both sides and sorted as the tree sorts them.</summary>
public abstract class ApiEntry
{
    private ApiEntry()
    {
    }

    /// <summary>What the entry is: a namespace, or a type or member of some shape.</summary>
    public required SymbolShape Shape { get; init; }

    public required string Text { get; init; }

    public required NodeAccessibility Accessibility { get; init; }

    /// <summary>How it differs from the left assembly to the right one, as the assemblies it is in say.</summary>
    public abstract ApiChange Change { get; }

    /// <summary>Whether it, or anything it holds, differs.</summary>
    public bool HasChanges => Change != ApiChange.Unchanged || _holdsChanges;

    // Worked out once from Children, as the tree asks HasChanges of every row it shows.
    private bool _holdsChanges;

    public required IReadOnlyList<ApiEntry> Children
    {
        get;
        init
        {
            field = [.. value];
            _holdsChanges = field.Any(static c => c.HasChanges);
        }
    }

    public sealed class Namespace : ApiEntry
    {
        public required ApiPresence Presence { get; init; }

        public override ApiChange Change => Presence switch
        {
            ApiPresence.LeftOnly => ApiChange.Removed,
            ApiPresence.RightOnly => ApiChange.Added,
            ApiPresence.Both => ApiChange.Unchanged,
            _ => throw new UnreachableException(),
        };
    }

    public sealed class Entity : ApiEntry
    {
        public required ApiSides Sides { get; init; }

        public override ApiChange Change => Sides.Change;
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Namespace, TResult> @namespace, Func<Entity, TResult> entity)
    {
        return this switch
        {
            Namespace c => @namespace(c),
            Entity c => entity(c),
            _ => throw new UnreachableException($"ApiEntry.Match does not handle {GetType().Name}."),
        };
    }
}
