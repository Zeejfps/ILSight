using ILSight.Core;

namespace ILSight;

/// <summary>What a run's arguments ask to open and show.</summary>
/// <param name="Paths">The files to open.</param>
/// <param name="NavigateTo">The documentation id of the namespace, type or member to show, such as
/// <c>T:System.String</c>, looked for in <paramref name="Paths"/>, or in every open assembly when there
/// are none; null to show the last file opened.</param>
public sealed record FilesToOpen(List<AssemblyPath> Paths, string? NavigateTo);
