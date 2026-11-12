using ILSight.Core;
using ILSight.Commanding;
using ILSight.AssemblyListArea;

namespace ILSight;

/// <summary>A list's assemblies <see cref="AssemblyLists"/> began reopening as a reload, for
/// <see cref="AssemblyLists.OpenListAsync"/> to open: the files, the reload they open as, which another reload or
/// clearing the list stops, and the loads closing the list before it started; and the list's analyses that wait to be
/// found, for the edge to start finding beside it (<see cref="Commands.StartOpeningList"/>). Dropped, the app says it
/// is reloading for good.</summary>
[MustUse]
public sealed class ListOpening
{
    public required IReadOnlyList<AssemblyPath> Paths { get; init; }

    public required ReloadTicket Reload { get; init; }

    public required TabLoads Closing { get; init; }

    public required AnalysesRestore? Analyses { get; init; }
}
