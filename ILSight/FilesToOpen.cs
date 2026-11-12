using ILSight.Core;

namespace ILSight;

/// <summary>What a run's arguments ask to open and show.</summary>
public sealed record FilesToOpen
{
    /// <summary>The files to open.</summary>
    public required List<AssemblyPath> Paths { get; init; }

    /// <summary>The documentation id of the namespace, type or member to show, such as <c>T:System.String</c>, looked
    /// for in <paramref name="Paths"/>, or in every open assembly when there are none; null to show the last file
    /// opened.</summary>
    public required string? NavigateTo { get; init; }
}
