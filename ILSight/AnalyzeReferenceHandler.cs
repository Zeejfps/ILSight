using ILSight.Core;

namespace ILSight;

/// <summary>Analyzes what a reference points at: finds it through <see cref="IPorts"/>, opening its assembly if it is
/// not, and turns the root waiting for it into its analysis.</summary>
public sealed class AnalyzeReferenceHandler(AnalyzeReferenceHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the reference leads nowhere.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The node <paramref name="reference"/> leads to, null when it leads nowhere, looked for without
        /// holding up the screen (it may read an assembly from disk); a lookup that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<BrowseNode?>> LookUpReferenceAsync(CodeReference.Navigable reference, CancellationToken ct);
    }

    /// <summary>Finds what the reference of <paramref name="pending"/> points at and turns its root into the analysis
    /// of it, which <paramref name="viewCommands"/> has the view show. The root goes when the reference does not
    /// resolve, which is reported; once the root was removed from <paramref name="analyzer"/>, as clearing the list
    /// removes them all, what is found is dropped.</summary>
    public async Task HandleAsync(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, PendingAnalysis pending, CancellationToken ct)
    {
        var found = await ports.LookUpReferenceAsync(pending.Reference, ct);
        if (!analyzer.HasRoot(pending.Root))
        {
            return;
        }

        var error = found.Match<Exception?>(
            ok =>
            {
                Analyze(analyzer, viewCommands, pending, ok.Value);
                return null;
            },
            static failed => failed.Error);
        if (error is null)
        {
            return;
        }

        analyzer.RemoveRoot(pending.Root);
        ReportUnresolved(pending.Reference, error);
    }

    private void Analyze(AnalyzerTree analyzer, Queue<ViewCommand> viewCommands, PendingAnalysis pending, BrowseNode? node)
    {
        if (node is null)
        {
            analyzer.RemoveRoot(pending.Root);
            ReportUnresolved(pending.Reference, null);
            return;
        }

        Analysis.AnalyzeFound(analyzer, viewCommands, pending.Root, node);
    }

    private void ReportUnresolved(CodeReference reference, Exception? error)
    {
        ports.ReportError(new AppProblem.Unresolved { Reference = reference, Error = error });
    }
}
