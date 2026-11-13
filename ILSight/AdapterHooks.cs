using ILSight.AssemblyArea;
using ILSight.DiagramArea;
using ILSight.SearchArea;

namespace ILSight;

/// <summary>What a host other than the app's window, a test, makes of adapters the root builds: each a function handed
/// the real adapter, which it may wrap (a decorator) or ignore (a fake); null keeps the real adapter.</summary>
public sealed record AdapterHooks
{
    /// <summary>The port the reopening of a list lists its files through.</summary>
    public Func<OpenAssembliesAsReloadHandler.IPorts, OpenAssembliesAsReloadHandler.IPorts>? Reopening { get; init; }

    /// <summary>The port a search's runs search the open assemblies through.</summary>
    public Func<RunSearchHandler.IPorts, RunSearchHandler.IPorts>? Searching { get; init; }

    /// <summary>The port a comparison's runs compare two assemblies through.</summary>
    public Func<CompareAssembliesHandler.IPorts, CompareAssembliesHandler.IPorts>? Comparing { get; init; }

    /// <summary>The port showing a type on a diagram reads its card and its neighbours' through.</summary>
    public Func<DiagramGrowing.IPorts, DiagramGrowing.IPorts>? Growing { get; init; }
}
