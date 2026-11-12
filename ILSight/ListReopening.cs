using ILSight.Core;

namespace ILSight;

/// <summary>The open list's assemblies <see cref="OpenListSession.BeginReopening"/> began reopening as a reload, for
/// the edge to open (<see cref="AssemblyArea.OpenAssembliesAsReloadHandler"/>): the files and the reload they open as,
/// which another reload or clearing the list stops. Dropped, the app says it is reloading for good.</summary>
[MustUse]
public sealed class ListReopening
{
    public required IReadOnlyList<AssemblyPath> Paths { get; init; }

    public required ReloadTicket Reload { get; init; }
}
