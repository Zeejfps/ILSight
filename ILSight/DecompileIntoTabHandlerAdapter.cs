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

    /// <summary>Makes it on the thread pool and comes back on the UI thread, a corrupt file the library failed on as
    /// <see cref="Outcome{T}.Failed"/>, as the core answers it; the cancellation of <paramref name="ct"/> is let
    /// through.</summary>
    public async Task<Outcome<ModuleRead<NodeContent>>> DecompileAsync(BrowseNode node, DecompilationLanguage language, DecompilerOptions options,
        int firstGridRows, CancellationToken ct)
    {
        var read = await Background.Wait(NodeContentLoader.ReadContentAsync(session, node, language, options, firstGridRows, ct));
        ct.ThrowIfCancellationRequested();
        return read;
    }
}
