using ILSight.Core;

namespace ILSight;

/// <summary>Reaches the decompiler over the browse tree on the thread pool, and the app's error reports, for
/// <see cref="DecompileIntoTabHandler"/>.</summary>
internal sealed class DecompileIntoTabHandlerAdapter(ErrorLog errors, BrowseSession session) : DecompileIntoTabHandler.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    /// <summary>Makes it on the thread pool and comes back on the UI thread, with what the read failed with (<see cref="UntrustedReads"/>) as
    /// <see cref="Outcome{T}.Failed"/>, the decompiler's lazy reads of a corrupt file among them, except the
    /// cancellation of <paramref name="ct"/>, which it lets through.</summary>
    public Task<Outcome<ModuleRead<NodeContent>>> DecompileAsync(BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        int firstGridRows, CancellationToken ct)
    {
        return UntrustedReads.CatchAsync(Background.Wait(NodeContentLoader.ReadContentAsync(session, node, language, options, firstGridRows, ct)), ct);
    }
}
