using ILSight.Core;

namespace ILSight;

/// <summary>Analyzes what a reference points at: finds it through <see cref="IPorts"/>, opening its assembly if it is
/// not, and turns the root waiting for it into its analysis.</summary>
public sealed class AnalyzeReferenceHandler(AnalyzeReferenceHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the reference leads nowhere, or into a module that could not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The node <paramref name="reference"/> leads to; not found when it leads nowhere, unreadable when
        /// into a module whose type system the library could not read; looked for without
        /// holding up the screen (it may read an assembly from disk); a lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Lookup<BrowseNode>>> LookUpReferenceAsync(CodeReference.Navigable reference, CancellationToken ct);
    }
    /// <summary>Finds what the reference of <paramref name="pending"/> points at and turns its root into the analysis
    /// of it, which <paramref name="viewCommands"/> has the view show. The root goes when the reference does not
    /// resolve, or leads into a module that could not be read, which is reported; once the root was removed from
    /// <paramref name="analyzer"/>, as clearing the list removes them all, what is found is dropped.</summary>
    public async Task HandleAsync(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, PendingAnalysis pending, CancellationToken ct)
    {
        var found = await ports.LookUpReferenceAsync(pending.Reference, ct);
        if (!analyzer.HasRoot(pending.Root))
        {
            return;
        }

        var problem = found.Match<AppProblem?>(
            ok => Analyze(analyzer, viewCommands, pending, ok.Value),
            failed => new AppProblem.Unresolved { Reference = pending.Reference, Error = failed.Error });
        if (problem is null)
        {
            return;
        }

        analyzer.RemoveRoot(pending.Root);
        ports.ReportError(problem);
    }

    /// <summary>Turns the root of <paramref name="pending"/> into the analysis of what <paramref name="lookup"/> found;
    /// else why not, for the root to go.</summary>
    private static AppProblem? Analyze(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, PendingAnalysis pending, Lookup<BrowseNode> lookup)
    {
        return lookup.Match<AppProblem?>(
            found =>
            {
                Analysis.AnalyzeFound(analyzer, viewCommands, pending.Root, found.Value);
                return null;
            },
            _ => new AppProblem.Unresolved { Reference = pending.Reference, Error = null },
            static unreadable => AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
    }
}
