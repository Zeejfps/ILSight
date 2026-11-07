using System.Diagnostics;

namespace ILSight.Core.Comparison;

/// <summary>Which types and members a comparison takes in; never those the compiler made up.</summary>
public enum ApiScope
{
    /// <summary>Only what other assemblies can use.</summary>
    PublicApi,

    /// <summary>Everything the source declared, public or not.</summary>
    AllDeclared,
}

/// <summary>How an entry differs from the left assembly to the right one.</summary>
public enum ApiChange
{
    Unchanged,

    /// <summary>On the right only.</summary>
    Added,

    /// <summary>On the left only.</summary>
    Removed,

    /// <summary>In both, declared differently: its accessibility, modifiers, type, base types, value, or
    /// whether it is obsolete or flags.</summary>
    Changed,
}

/// <summary>Which of the assemblies compared a namespace is in.</summary>
public enum ApiPresence
{
    LeftOnly,
    RightOnly,
    Both,
}

/// <summary>A type or member as one assembly has it: what it points at, and how it is declared, as C# writes it.</summary>
public sealed record ApiSide(CodeReference.Entity Reference, string Declaration);

/// <summary>Which of the assemblies compared a type or member is in, and how each has it.</summary>
public abstract record ApiSides
{
    private ApiSides()
    {
    }

    public sealed record LeftOnly(ApiSide Left) : ApiSides;

    public sealed record RightOnly(ApiSide Right) : ApiSides;

    public sealed record Both(ApiSide Left, ApiSide Right) : ApiSides;
}

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

    public required ApiChange Change { get; init; }

    /// <summary>Whether it, or anything it holds, differs.</summary>
    public required bool HasChanges { get; init; }

    public required IReadOnlyList<ApiEntry> Children { get; init; }

    public sealed class Namespace : ApiEntry
    {
        public required ApiPresence Presence { get; init; }
    }

    public sealed class Entity : ApiEntry
    {
        public required ApiSides Sides { get; init; }
    }
}

/// <summary>What changed in the API from a left assembly to a right one: their namespaces, merged
/// from both and sorted by name, and how many types and members were added, removed and changed, each
/// counted once with what it holds.</summary>
public sealed class ApiDiff
{
    /// <summary>The left assembly's name and version.</summary>
    public required string LeftName { get; init; }

    /// <summary>The right assembly's name and version.</summary>
    public required string RightName { get; init; }

    public required ApiScope Scope { get; init; }

    public required IReadOnlyList<ApiEntry> Namespaces { get; init; }

    public required int Added { get; init; }

    public required int Removed { get; init; }

    public required int Changed { get; init; }
}
