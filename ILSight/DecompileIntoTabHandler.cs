using ILSight.Core;

namespace ILSight;

/// <summary>Brings in what a tab was pointed at (<see cref="Decompilation.StartDecompiling"/>): the node decompiled,
/// or its grid or picture read, then shows it in the tab and commands the view where to stand in it. Reaches the
/// decompiler and the user's error reports through <see cref="IPorts"/>.</summary>
public sealed class DecompileIntoTabHandler(DecompileIntoTabHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user what a tab was to show could not be shown.</summary>
        void ReportError(AppProblem problem);

        /// <summary>What <paramref name="node"/> shows in <paramref name="language"/> with <paramref name="options"/>:
        /// its code, grid or picture, made without holding up the screen. What making it threw, such as a corrupt
        /// file, comes back as <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let
        /// through.</summary>
        Task<Outcome<NodeContent>> DecompileAsync(BrowseNode node, DecompilationLanguage language, DecompilerOptions options, CancellationToken ct);
    }

    /// <summary>Brings in what <paramref name="ticket"/> started loading in its tab and shows it there, or that it
    /// failed, which is reported unless the node's file failed to load, which the tab tells already; then commands
    /// the view, through <paramref name="viewCommands"/>, to stand where the load was to arrive, or on the node's
    /// definition when it failed. Drops it when newer work started on the tab, or the tab closed, meanwhile.
    /// <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the ticket's work.</summary>
    public async Task HandleAsync(Queue<ViewCommand> viewCommands, DecompileTicket ticket, CancellationToken ct)
    {
        var loaded = await ports.DecompileAsync(ticket.Node, ticket.Language, ticket.Options, ct);
        if (!ticket.Tab.Work.TryFinish(ticket.Work))
        {
            return;
        }

        var tab = ticket.Tab;
        var failure = ShowLoaded(tab, loaded);
        var arrival = Tabs.TakeArrival(tab);
        if (failure is not null)
        {
            ReportFailure(ticket.Node, failure);
            Decompilation.Arrive(viewCommands, tab, new Arrival());
            return;
        }
        Decompilation.Arrive(viewCommands, tab, arrival);
    }

    /// <summary>Shows in <paramref name="tab"/> what its load brought, or that it failed. Returns why it failed, or
    /// why the document it brought could not be decompiled; null when neither.</summary>
    private static Exception? ShowLoaded(DocumentTab tab, Outcome<NodeContent> loaded)
    {
        return loaded.Match<Exception?>(
            ok =>
            {
                Tabs.ShowLoaded(tab, ok.Value);
                return tab.Document.Error;
            },
            failed =>
            {
                Tabs.ShowFailed(tab, failed.Error.Message);
                return failed.Error;
            });
    }

    /// <summary>Reports why <paramref name="node"/> could not be shown, unless its file failed to load,
    /// which its tab tells already.</summary>
    private void ReportFailure(BrowseNode node, Exception error)
    {
        if (node.Assembly is FailedAssembly)
        {
            return;
        }

        ports.ReportError(new AppProblem.DecompilationFailed(BrowseNodes.GetFullName(node), error));
    }
}
