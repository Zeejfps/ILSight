namespace ILSight.Core.Comparison;

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
