using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>Runs one analysis <see cref="AnalyzerKindNode.StartRun"/> started over the open assemblies through
/// <see cref="IPorts"/>, without holding up the screen, showing what it finds as it comes in; when it fails, it ends
/// with a row telling why, and the whole error is reported.</summary>
public sealed class RunAnalysisHandler(RunAnalysisHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user an analysis failed.</summary>
        void ReportError(AppProblem problem);

        /// <summary>What <paramref name="kind"/> finds for <paramref name="subject"/> in the open assemblies, in
        /// batches as they are found, and after them each module it reached that the library could not read, worked
        /// out without holding up the screen. Working it out that failed ends the sequence with
        /// <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let through.</summary>
        IAsyncEnumerable<Outcome<IReadOnlyList<AnalysisFind>>> RunAsync(AnalysisSymbol subject, AnalysisKind kind, CancellationToken ct);
    }

    /// <summary>Runs <paramref name="run"/>, adding what it finds to its analysis in <paramref name="analyzer"/> as it
    /// comes in, and ends it done, or with a row telling why it failed, which is reported. <paramref name="ct"/> is the
    /// workflow's token, which the edge that starts it links to the run's work.</summary>
    public async Task HandleAsync(AnalyzerTree analyzer, AnalysisRun run, CancellationToken ct)
    {
        var kind = run.Kind;
        await foreach (var found in ports.RunAsync(kind.Subject, kind.Kind, ct))
        {
            var error = found.Match<Exception?>(
                ok =>
                {
                    Analysis.AddFound(analyzer, run, ok.Value);
                    return null;
                },
                static failed => failed.Error);
            if (error is not null)
            {
                Fail(analyzer, run, error);
                return;
            }
        }

        Analysis.FinishRun(analyzer, run, error: null);
    }

    private void Fail(AnalyzerTree analyzer, AnalysisRun run, Exception error)
    {
        Analysis.FinishRun(analyzer, run, error.Message);
        ports.ReportError(new AppProblem.AnalysisFailed
        {
            Subject = run.Kind.Subject.Label,
            Kind = run.Kind.Kind,
            Error = error,
        });
    }
}
