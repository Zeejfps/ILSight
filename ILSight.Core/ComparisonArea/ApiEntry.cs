namespace ILSight.Core.ComparisonArea;

/// <summary>A namespace, type or member of either assembly compared, labelled as the tree labels it,
/// and what it holds, merged from both sides and sorted as the tree sorts them.</summary>
public sealed class ApiEntry
{
    /// <summary>What the entry is: a namespace, or a type or member of some shape.</summary>
    public required SymbolShape Shape { get; init; }

    public required string Text { get; init; }

    public required NodeAccessibility Accessibility { get; init; }

    /// <summary>A namespace, and the assemblies it is in, or a type or member, and how each side has it.</summary>
    public required ApiEntryKind Kind { get; init; }

    /// <summary>How it differs from the left assembly to the right one, as the assemblies it is in say.</summary>
    public ApiChange Change => Kind.Change;

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
}
