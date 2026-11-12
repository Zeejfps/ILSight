using ILSight.Core;
using ILSight.Core.Comparison;

namespace ILSight.AssemblyArea;

/// <summary>Runs a comparison <see cref="Comparisons"/> started: compares its two assemblies' APIs through
/// <see cref="IPorts"/>, without holding up the screen, and shows what changed, or that it failed, unless a newer
/// comparison of it started or its tab closed meanwhile.</summary>
public sealed class CompareAssembliesHandler(CompareAssembliesHandler.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>Tells the user two assemblies could not be compared.</summary>
        void ReportError(AppProblem problem);

        /// <summary>What changed from <paramref name="left"/>'s API to <paramref name="right"/>'s, taking in what
        /// <paramref name="scope"/> says, worked out without holding up the screen. Working it out that failed comes back
        /// as <see cref="Outcome{T}.Failed"/>; the cancellation of <paramref name="ct"/> is let through.</summary>
        Task<Outcome<ApiDiff>> CompareAsync(LoadedModule left, LoadedModule right, ApiScope scope, CancellationToken ct);
    }

    /// <summary>Compares the assemblies of <paramref name="run"/> and shows what changed in its comparison, or that it
    /// failed, which is reported; drops it when a newer comparison of it started or it was closed meanwhile.
    /// <paramref name="ct"/> is the workflow's token, which the edge that starts it links to the run's work.</summary>
    public async Task HandleAsync(ComparisonRun run, CancellationToken ct)
    {
        var compared = await ports.CompareAsync(run.Left, run.Right, run.Scope, ct);
        var comparison = run.Comparison;
        if (!comparison.Work.TryFinish(run.Work))
        {
            return;
        }

        var error = compared.Match<Exception?>(
            ok =>
            {
                comparison.ShowDiff(ok.Value);
                return null;
            },
            static failed => failed.Error);
        if (error is null)
        {
            return;
        }

        ShowFailure(run, error);
    }

    private void ShowFailure(ComparisonRun run, Exception error)
    {
        var left = run.Left.NameAndVersion;
        var right = run.Right.NameAndVersion;
        run.Comparison.ShowFailure(new ComparisonFailure.Failed(left, right));
        ports.ReportError(new AppProblem.ComparisonFailed(left, right, error));
    }
}
