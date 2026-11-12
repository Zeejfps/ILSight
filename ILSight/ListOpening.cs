using ILSight.Core;

namespace ILSight;

/// <summary>A list's assemblies <see cref="AssemblyLists"/> began reopening as a reload, for
/// <see cref="AssemblyLists.OpenListAsync"/> to open: the files, the reload they open as, which another reload or
/// clearing the list stops, and the loads closing the list before it started. Dropped, the app says it is reloading
/// for good.</summary>
[MustUse]
public sealed class ListOpening
{
    public required IReadOnlyList<AssemblyPath> Paths { get; init; }

    public required ReloadTicket Reload { get; init; }

    public required TabLoads Closing { get; init; }
}
