using ILSight.Core;

namespace ILSight.AssemblyArea;

/// <summary>A reload of the open list begun (<see cref="AssemblyReloader.BeginReload"/>): the files to read again, the
/// tabs whose places to find in them, the request it began, and the work of the list's generation its clear began, which
/// the reopen carries (<see cref="AssemblyReloader.RefreshAssembliesAsync"/>): the edge links and asks its ending, so
/// any end of that generation ends the reopen quietly, whether or not it stops the reload. Dropped, the tabs stay
/// reloading for good.</summary>
[MustUse]
public sealed class ReloadBegun
{
    public required IReadOnlyList<AssemblyPath> Paths { get; init; }

    public required IReadOnlyList<ReloadingTab> Tabs { get; init; }

    /// <summary>The navigation request the reload began, which a tab's place revealed in the tree belongs to.</summary>
    public required int Intent { get; init; }

    public required GenerationWork Work { get; init; }
}
