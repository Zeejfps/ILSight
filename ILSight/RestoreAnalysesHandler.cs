using ILSight.Core;
using ILSight.Core.AnalysisArea;

namespace ILSight;

/// <summary>Analyzes again what was analyzed in the last run: finds the symbols of the roots restored from the saved
/// analyses through <see cref="IPorts"/>, and turns each root into the analysis of its symbol, or, when its symbol is
/// gone, in a module that could not be read or failed to be found, which is reported, keeps it as saved, failed with
/// why, to be looked for again once its assemblies are read again.</summary>
public sealed class RestoreAnalysesHandler(RestoreAnalysesHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user the saved analyses could not be found, or are in a module that could not be
        /// read.</summary>
        void ReportError(AppProblem problem);

        /// <summary>What looking for the type or member each of <paramref name="analyzed"/> names came to, in order:
        /// not found where it is gone, unreadable where the library could not read its module's type system,
        /// <see cref="Outcome{T}.Failed"/> where finding it failed; found without holding up the screen by reading their
        /// assemblies' files without listing them.</summary>
        Task<List<Outcome<Lookup<AnalysisSymbol>>>> LookUpAnalyzedAsync(IReadOnlyList<CodeAnchor> analyzed, CancellationToken ct);
    }

    /// <summary>Finds the symbols of the roots of <paramref name="restore"/> and turns each into the analysis of its
    /// symbol in <paramref name="analyzer"/>, which <paramref name="viewCommands"/> has the view show, or keeps it as
    /// saved, failed with why, when its symbol is gone, in a module that could not be read, which it names, or failed
    /// to be found, which is reported once; leaves them alone when <paramref name="assemblies"/> was cleared since
    /// <paramref name="restore"/> began, reading nothing when that was before it starts (the app's start, its arguments
    /// switching or clearing the list first).</summary>
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

        Analysis.AnalyzeAgain(analyzer, viewCommands, restore.Roots, found);
        ReportUnreadable(Lookup.ListUnreadable(ListLooked(found)));
        foreach (var error in ListErrors(found))
        {
            ports.ReportError(new AppProblem.AnalysesNotRestored(error));
        }
    }

    // What the lookups that did not fail came to, in order.
    private static List<Lookup<AnalysisSymbol>> ListLooked(List<Outcome<Lookup<AnalysisSymbol>>> found)
    {
        var looked = new List<Lookup<AnalysisSymbol>>();
        foreach (var outcome in found)
        {
            if (outcome is Outcome<Lookup<AnalysisSymbol>>.Ok ok)
            {
                looked.Add(ok.Value);
            }
        }
        return looked;
    }

    // What the lookups that failed failed with, in order.
    private static List<Exception> ListErrors(List<Outcome<Lookup<AnalysisSymbol>>> found)
    {
        var errors = new List<Exception>();
        foreach (var outcome in found)
        {
            if (outcome is Outcome<Lookup<AnalysisSymbol>>.Failed failed)
            {
                errors.Add(failed.Error);
            }
        }
        return errors;
    }

    private void ReportUnreadable(List<UnreadableModule> unreadable)
    {
        foreach (var module in unreadable)
        {
            ports.ReportError(AppProblem.ModuleUnreadable.FromModule(module));
        }
    }
}
