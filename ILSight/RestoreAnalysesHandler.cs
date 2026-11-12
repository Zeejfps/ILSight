using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>Analyzes again what was analyzed in the last run: finds the symbols of the roots restored from the saved
/// analyses through <see cref="IPorts"/>, and turns each root into the analysis of its symbol, or removes it when its
/// symbol is gone.</summary>
public sealed class RestoreAnalysesHandler(RestoreAnalysesHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the saved analyses could not be found.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The type or member each of <paramref name="analyzed"/> names, in order, null where it is gone,
        /// found without holding up the screen by reading their assemblies' files without listing them; finding them
        /// that failed comes back as <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<List<AnalysisSymbol?>>> LookUpAnalyzedAsync(IReadOnlyList<CodeAnchor> analyzed, CancellationToken ct);
    }

    /// <summary>Finds the symbols of the roots of <paramref name="restore"/> and turns each into the analysis of its
    /// symbol in <paramref name="analyzer"/>, which <paramref name="viewCommands"/> has the view show, or removes it
    /// when its symbol is gone; drops them all when <paramref name="assemblies"/> was cleared meanwhile. Reports why
    /// the symbols could not be found, and removes the roots then.</summary>
    public async Task HandleAsync(AnalyzerTree analyzer, AssemblyList assemblies, Queue<ViewCommand> viewCommands, AnalysesRestore restore,
        CancellationToken ct)
    {
        var found = await ports.LookUpAnalyzedAsync(restore.Analyzed, ct);
        if (assemblies.Generation != restore.Generation)
        {
            return;
        }

        var error = found.Match<Exception?>(
            ok =>
            {
                Analysis.AnalyzeAgain(analyzer, viewCommands, restore.Roots, ok.Value);
                return null;
            },
            static failed => failed.Error);
        if (error is null)
        {
            return;
        }

        ports.ReportError(new AppProblem.AnalysesNotRestored(error));
        Analysis.RemoveRoots(analyzer, restore.Roots);
    }
}
