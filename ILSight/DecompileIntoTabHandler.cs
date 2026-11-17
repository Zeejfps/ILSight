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

        /// <summary>What <paramref name="node"/> shows in <paramref name="language"/> with <paramref name="options"/>,
        /// the app's notes beside its code in <paramref name="notes"/>' words: its code, grid or picture (a grid made with at least <paramref name="firstGridRows"/> rows read), made
        /// without holding up the screen, or that its module was closed before it began, and nothing was read. What
        /// making it threw, such as a corrupt file, comes back as <see cref="Outcome{T}.Failed"/>; the cancellation of
        /// <paramref name="ct"/> is let through.</summary>
        Task<Outcome<ModuleRead<NodeContent>>> DecompileAsync(BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
            IDocumentNotes notes, int firstGridRows, CancellationToken ct);
    }

    /// <summary>Brings in what <paramref name="ticket"/> started loading in its tab and shows it there, or that it
    /// failed, which is reported unless the node's file failed to load, which the tab tells already, and each reference
    /// its code read by name only because its file could not be read, the first time any tab shows it; then commands
    /// the view, through <paramref name="viewCommands"/>, to stand where the load was to arrive, or on the node's
    /// definition when it failed. Drops it when newer work started on the tab, or the tab closed, meanwhile. When the
    /// node's module was closed before the decompile began, the tab keeps what it shows and says its assembly was
    /// closed, as closing it does.
    /// A grid it brings has its first <paramref name="firstGridRows"/> rows read, what the screen's first show of it
    /// needs. <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the ticket's
    /// work. The notes the app writes beside the code are in <paramref name="notes"/>' words, the app's language as the
    /// load starts.</summary>
    public async Task HandleAsync(Queue<ViewCommand> viewCommands, DecompileTicket ticket, IDocumentNotes notes, int firstGridRows, CancellationToken ct)
    {
        var read = await ports.DecompileAsync(ticket.Node, ticket.Language, ticket.Options, notes, firstGridRows, ct);
        var load = ticket.Load;
        var shownOrLoaded = ShowClosedOrFindLoaded(load, ticket.Work, read);
        if (shownOrLoaded is null)
        {
            return;
        }

        var loaded = shownOrLoaded.Value;
        if (!TryShowLoaded(load, ticket.Work, loaded))
        {
            return;
        }

        ReportUnreadReferences(load.Document);
        ReportPartFailures(ticket.Node, load.Document);
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

    private static Outcome<NodeContent>? ShowClosedOrFindLoaded(TabLoad load, WorkTicket work, Outcome<ModuleRead<NodeContent>> read)
    {
        return read switch
        {
            Outcome<ModuleRead<NodeContent>>.Ok { Value: ModuleRead<NodeContent>.Read content } => new Outcome<NodeContent>.Ok(content.Value),
            Outcome<ModuleRead<NodeContent>>.Ok { Value: ModuleRead<NodeContent>.Closed closed } => ShowClosed(load, work, closed.Module),
            Outcome<ModuleRead<NodeContent>>.Failed failed => new Outcome<NodeContent>.Failed(failed.Error),
        };
    }

    private static Outcome<NodeContent>? ShowClosed(TabLoad load, WorkTicket work, LoadedModule module)
    {
        load.TryShowClosed(work, module.FilePath.FileName);
        return null;
    }

    private static bool TryShowLoaded(TabLoad load, WorkTicket work, Outcome<NodeContent> loaded)
    {
        return loaded switch
        {
            Outcome<NodeContent>.Ok ok => load.TryShowLoaded(work, ok.Value),
            Outcome<NodeContent>.Failed failed => load.TryShowFailed(work, failed.Error),
        };
    }

    private static Exception? FindFailure(TabLoad load, Outcome<NodeContent> loaded)
    {
        return loaded switch
        {
            Outcome<NodeContent>.Ok => load.Document.Error,
            Outcome<NodeContent>.Failed failed => failed.Error,
        };
    }

    // A reference the code read by name only, its file there but unreadable, is reported once, by the first tab to show it.
    private void ReportUnreadReferences(DecompiledDocument document)
    {
        foreach (var unread in document.UnreadReferences)
        {
            if (unread.TryTakeReport())
            {
                ports.ReportError(LookupFailures.OfUnreadReference(unread));
            }
        }
    }

    // A part of the code that failed where the rest is shown (a method's C# above its IL) is the decompilation's failure,
    // said in its place in the text and reported with its exception.
    private void ReportPartFailures(BrowseNode node, DecompiledDocument document)
    {
        foreach (var error in document.PartFailures)
        {
            ports.ReportError(new AppProblem.DecompilationFailed { Node = node.FullName, Error = error });
        }
    }

    private void ReportFailure(BrowseNode node, Exception error)
    {
        // A file that failed to load has its tab tell it already.
        if (node.Assembly is FailedAssembly)
        {
            return;
        }

        ports.ReportError(new AppProblem.DecompilationFailed { Node = node.FullName, Error = error });
    }
}
