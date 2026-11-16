using ILSight.Core;

namespace ILSight;

/// <summary>The open list's assemblies <see cref="OpenListSession.BeginReopening"/> began reopening as a reload, for
/// the edge to open (<see cref="AssemblyArea.OpenAssembliesAsReloadHandler"/>): the files, the reload they open as,
/// which another reload or clearing the list stops, and the work of the list's generation they open in, whose ending
/// the edge links and asks, so any end of it stops them quietly. Dropped, the app says it is reloading for good.</summary>
[MustUse]
public sealed class ListReopen
{
    public required IReadOnlyList<AssemblyPath> Paths { get; init; }

    public required ReloadTicket Reload { get; init; }

    public required GenerationWork Work { get; init; }
}
