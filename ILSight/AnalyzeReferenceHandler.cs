using ILSight.Core;

namespace ILSight;

/// <summary>Analyzes what a reference points at, or a symbol whose assembly is not on the list, by its own reference:
/// finds it through <see cref="IPorts"/>, opening and listing its assembly if it is not, and turns the root waiting for
/// it into its analysis.</summary>
public sealed class AnalyzeReferenceHandler(AnalyzeReferenceHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user looking for the reference failed, or it leads into a module that could not be read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>Tells the user what they asked for was refused, and why, where they acted.</summary>
        void ShowRefusal(Refusal refusal);


        /// <summary>The node <paramref name="reference"/> leads to; not found when it leads nowhere, unreadable when
        /// into a module whose type system the library could not read; looked for without
        /// holding up the screen (it may read an assembly from disk); a lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<Lookup<BrowseNode>>> LookUpReferenceAsync(CodeReference.Navigable reference, CancellationToken ct);
    }
    /// <summary>Finds what the reference of <paramref name="pending"/> points at, listing its assembly, and turns its
    /// root into the analysis it waits for (<see cref="PendingAnalysis.Analyze"/>), which
    /// <paramref name="viewCommands"/> has the view show. The root goes when the reference does not resolve, or leads
    /// into a module that could not be read, which is reported; once the root was removed from
    /// <paramref name="analyzer"/>, as clearing the list removes them all, what is found is dropped.</summary>
    public async Task HandleAsync(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, PendingAnalysis pending, CancellationToken ct)
    {
        var found = await ports.LookUpReferenceAsync(pending.Reference, ct);
        if (!analyzer.HasRoot(pending.Root))
        {
            return;
        }

        found.Switch(
            ok => Analyze(analyzer, viewCommands, pending, ok.Value),
            failed =>
            {
                analyzer.RemoveRoot(pending.Root);
                ports.ReportError(new AppProblem.Unresolved { Reference = pending.Reference, Error = failed.Error });
            });
    }

    private void Analyze(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, PendingAnalysis pending, Lookup<BrowseNode> lookup)
    {
        lookup.Switch(
            found => pending.Analyze(analyzer, viewCommands, found.Value),
            _ =>
            {
                analyzer.RemoveRoot(pending.Root);
                ports.ShowRefusal(new Refusal.Unresolved { Reference = pending.Reference });
            },
            unreadable =>
            {
                analyzer.RemoveRoot(pending.Root);
                ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
            });
    }
}
