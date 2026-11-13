using ILSight.Core;
using ILSight.Core.Analysis;

namespace ILSight;

/// <summary>Analyzes again what was analyzed in the last run: finds the symbols of the roots restored from the saved
/// analyses through <see cref="IPorts"/>, and turns each root into the analysis of its symbol, or, when its symbol is
/// gone or in a module that could not be read, which is reported, keeps it as saved, failed with why, to be looked for
/// again once its assemblies are read again.</summary>
public sealed class RestoreAnalysesHandler(RestoreAnalysesHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the saved analyses could not be found, or are in a module that could not be
        /// read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>The type or member each of <paramref name="analyzed"/> names, in order: not found where it is
        /// gone, unreadable where the library could not read its module's type system; found without holding up the
        /// screen by reading their assemblies' files without listing them; finding them that failed comes back as
        /// <see cref="Outcome{T}.Failed"/>.</summary>
        Task<Outcome<List<Lookup<AnalysisSymbol>>>> LookUpAnalyzedAsync(IReadOnlyList<CodeAnchor> analyzed, CancellationToken ct);
    }

    /// <summary>Finds the symbols of the roots of <paramref name="restore"/> and turns each into the analysis of its
    /// symbol in <paramref name="analyzer"/>, which <paramref name="viewCommands"/> has the view show, or keeps it as
    /// saved, failed with why, when its symbol is gone or in a module that could not be read, which it names; leaves
    /// them alone when <paramref name="assemblies"/> was cleared since <paramref name="restore"/> began, reading
    /// nothing when that was before it starts (the app's start, its arguments switching or clearing the list first).
    /// Reports why the symbols could not be found, and keeps the roots as saved, failed with that, then.</summary>
    public async Task HandleAsync(AnalyzerTree analyzer, AssemblyList assemblies, Queue<ViewCommand> viewCommands, AnalysesRestore restore,
        CancellationToken ct)
    {
        if (assemblies.Generation != restore.Generation)
        {
            return;
        }

        var found = await ports.LookUpAnalyzedAsync(restore.Analyzed, ct);
        if (assemblies.Generation != restore.Generation)
        {
            return;
        }

        var error = found.Match<Exception?>(
            ok =>
            {
                Analysis.AnalyzeAgain(analyzer, viewCommands, restore.Roots, ok.Value);
                ReportUnreadable(Lookup.ListUnreadable(ok.Value));
                return null;
            },
            static failed => failed.Error);
        if (error is null)
        {
            return;
        }

        ports.ReportError(new AppProblem.AnalysesNotRestored(error));
        Analysis.FailRoots(analyzer, restore.Roots, error.Message);
    }

    private void ReportUnreadable(List<UnreadableModule> unreadable)
    {
        foreach (var module in unreadable)
        {
            ports.ReportError(AppProblem.ModuleUnreadable.FromModule(module));
        }
    }
}
