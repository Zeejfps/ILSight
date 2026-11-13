using ILSight.CommandArea;
using ILSight.Core;

namespace ILSight;

/// <summary>What closing every assembly of the open list hands back (<see cref="OpenListSession.CloseAllAssemblies"/>):
/// the loads that started in its tabs, for the edge to start, and the modules taken off, for it to have released
/// (<see cref="Commands.FinishClosing"/>). Dropped, the tabs wait for good and the files stay held.</summary>
[MustUse]
public sealed class ListClosing
{
    public required TabLoads Loads { get; init; }

    public required RemovedModules Removed { get; init; }
}
