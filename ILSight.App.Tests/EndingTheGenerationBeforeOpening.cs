using ILSight.AssemblyArea;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>The port the root hands a list's reopening (<see cref="AdapterHooks.Reopening"/>): the real adapter, but the
/// opening first runs <see cref="End"/> on the thread that owns the list, as a change that ends the list's generation and
/// stops no reload would, between the reopening's start and its check.</summary>
internal sealed class EndingTheGenerationBeforeOpening(OpenAssembliesAsReloadHandler.IPorts real) : OpenAssembliesAsReloadHandler.IPorts
{
    public Action End { get; set; } = static () => { };

    public Task<LoadedAssembly?> OpenFilesAsync(IReadOnlyList<AssemblyPath> paths, GenerationWork work, CancellationToken ct)
    {
        End();
        return real.OpenFilesAsync(paths, work, ct);
    }
}
