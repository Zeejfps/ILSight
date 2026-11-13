using ILSight.CommandArea;
using ILSight.AssemblyListArea;

namespace ILSight;

/// <summary>A list <see cref="AssemblyLists"/> switched to and began opening: its assemblies, begun reopening as a
/// reload, for the edge to open (<see cref="Commands.StartOpeningList"/>); the loads closing the list before it
/// started; and the list's analyses that wait to be found, for the edge to start finding beside it. Dropped, the app
/// says it is reloading for good.</summary>
[MustUse]
public sealed class ListOpening
{
    public required ListReopening Reopening { get; init; }

    public required ListClosing Closing { get; init; }

    public required AnalysesRestore? Analyses { get; init; }
}
