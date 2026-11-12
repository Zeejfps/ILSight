using ILSight.Core.Processes;
using ILSight.ProcessBrowsing;

namespace ILSight;

/// <summary>A listing of a process's modules <see cref="ProcessBrowser.StartListing"/> started, for
/// <see cref="ProcessBrowser.ListModulesAsync"/> to run: the process, and the work it runs as, which picking another
/// process or closing the dialog cancels.</summary>
[MustUse]
public sealed class ModuleListingTicket
{
    public required RunningProcess Process { get; init; }

    public required WorkTicket Work { get; init; }
}
