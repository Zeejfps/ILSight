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
    /// <paramref name="viewCommands"/> has the view show. The root goes when the reference does not resolve (refused
    /// where the user acted); it shows failed with why, in its place, when it leads into a module that could not be
    /// read or looking for it failed, which is reported; once the root was removed from
    /// <paramref name="analyzer"/>, as clearing the list removes them all, what is found is dropped.</summary>
    public async Task HandleAsync(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, PendingAnalysis pending, CancellationToken ct)
    {
        var found = await ports.LookUpReferenceAsync(pending.Reference, ct);
        if (!analyzer.HasRoot(pending.Root))
        {
            return;
        }

        switch (found)
        {
            case Outcome<Lookup<BrowseNode>>.Ok ok:
                Analyze(analyzer, viewCommands, pending, ok.Value);
                break;
            case Outcome<Lookup<BrowseNode>>.Failed failed:
                analyzer.FailUnresolved(pending.Root, pending.Reference.Name, failed.Error);
                ports.ReportError(LookupFailures.OfReference(pending.Reference, failed.Error));
                break;
        }
    }

    private void Analyze(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, PendingAnalysis pending, Lookup<BrowseNode> lookup)
    {
        switch (lookup)
        {
            case Lookup<BrowseNode>.Found found:
                pending.Analyze(analyzer, viewCommands, found.Value);
                break;
            case Lookup<BrowseNode>.NotFound:
                analyzer.RemoveRoot(pending.Root);
                ports.ShowRefusal(new Refusal.Unresolved { Reference = pending.Reference });
                break;
            case Lookup<BrowseNode>.Unreadable unreadable:
                analyzer.FailUnresolved(pending.Root, unreadable.Module.Module.FilePath.FileName, unreadable.Module.Why);
                ports.ReportError(AppProblem.ModuleUnreadable.FromModule(unreadable.Module));
                break;
        }
    }
}
