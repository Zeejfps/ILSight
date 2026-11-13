using ILSight.Core;

namespace ILSight;

/// <summary>Brings in what a tab was pointed at (<see cref="Navigation"/>): the node decompiled,
/// or its grid or picture read, then shows it in the tab and commands the view where to stand in it. Reaches the
/// decompiler and the user's error reports through <see cref="IPorts"/>.</summary>
public sealed class DecompileIntoTabHandler(DecompileIntoTabHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user what a tab was to show could not be shown.</summary>
        void ReportError(AppProblem problem);

        /// <summary>What <paramref name="node"/> shows in <paramref name="language"/> with <paramref name="options"/>:
        /// its code, grid or picture (a grid made with at least <paramref name="firstGridRows"/> rows read), made
        /// without holding up the screen, or that its module was closed before it began, and nothing was read. What
        /// making it threw, such as a corrupt file, comes back as <see cref="Outcome{T}.Failed"/>; the cancellation of
        /// <paramref name="ct"/> is let through.</summary>
        Task<Outcome<ModuleRead<NodeContent>>> DecompileAsync(BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
            int firstGridRows, CancellationToken ct);
    }

    /// <summary>Brings in what <paramref name="ticket"/> started loading in its tab and shows it there, or that it
    /// failed, which is reported unless the node's file failed to load, which the tab tells already; then commands
    /// the view, through <paramref name="viewCommands"/>, to stand where the load was to arrive, or on the node's
    /// definition when it failed. Drops it when newer work started on the tab, or the tab closed, meanwhile. When the
    /// node's module was closed before the decompile began, the tab keeps what it shows and says its assembly was
    /// closed, as closing it does.
    /// A grid it brings has its first <paramref name="firstGridRows"/> rows read, what the screen's first show of it
    /// needs. <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the ticket's
    /// work.</summary>
    public async Task HandleAsync(Queue<ViewCommand> viewCommands, DecompileTicket ticket, int firstGridRows, CancellationToken ct)
    {
        var read = await ports.DecompileAsync(ticket.Node, ticket.Language, ticket.Options, firstGridRows, ct);
        var load = ticket.Load;
        var loaded = FindLoaded(load, ticket.Work, read);
        if (loaded is null || !TryShowLoaded(load, ticket.Work, loaded))
        {
            return;
        }

        var tab = ticket.Tab;
        var arrival = load.TakeArrival();
        var failure = FindFailure(load, loaded);
        if (failure is not null)
        {
            ReportFailure(ticket.Node, failure);
            Decompilation.Arrive(viewCommands, tab, new Arrival());
            return;
        }
        Decompilation.Arrive(viewCommands, tab, arrival);
    }

    /// <summary>What the decompile brought, or that it failed; null when the node's module was closed before it began,
    /// which <paramref name="load"/> then shows, unless a newer load started on the tab, or the tab closed, since
    /// <paramref name="work"/> was given.</summary>
    private static Outcome<NodeContent>? FindLoaded(TabLoad load, WorkTicket work, Outcome<ModuleRead<NodeContent>> read)
    {
        return read.Match<Outcome<NodeContent>?>(
            ok => ok.Value.Match<Outcome<NodeContent>?>(
                static content => new Outcome<NodeContent>.Ok(content.Value),
                closed => ShowClosed(load, work, closed.Module)),
            static failed => new Outcome<NodeContent>.Failed(failed.Error));
    }

    private static Outcome<NodeContent>? ShowClosed(TabLoad load, WorkTicket work, LoadedModule module)
    {
        load.TryShowClosed(work, module.FilePath.FileName);
        return null;
    }

    /// <summary>Shows on <paramref name="load"/> what it brought, or that it failed, unless a newer load started on the
    /// tab, or the tab closed, since <paramref name="work"/> was given. Returns whether it landed.</summary>
    private static bool TryShowLoaded(TabLoad load, WorkTicket work, Outcome<NodeContent> loaded)
    {
        return loaded.Match(
            ok => load.TryShowLoaded(work, ok.Value),
            failed => load.TryShowFailed(work, failed.Error.Message));
    }

    /// <summary>Why the load failed, or why the document it brought could not be decompiled; null when neither.</summary>
    private static Exception? FindFailure(TabLoad load, Outcome<NodeContent> loaded)
    {
        return loaded.Match<Exception?>(
            _ => load.Document.Error,
            failed => failed.Error);
    }

    /// <summary>Reports why <paramref name="node"/> could not be shown, unless its file failed to load,
    /// which its tab tells already.</summary>
    private void ReportFailure(BrowseNode node, Exception error)
    {
        if (node.Assembly is FailedAssembly)
        {
            return;
        }

        ports.ReportError(new AppProblem.DecompilationFailed { Node = node.FullName, Error = error });
    }
}
