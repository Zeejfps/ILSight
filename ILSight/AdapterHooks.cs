using ILSight.AssemblyArea;
using ILSight.BookmarkArea;
using ILSight.DiagramArea;
using ILSight.ExportArea;
using ILSight.ILSightAppArea;
using ILSight.MainArea;
using ILSight.SearchArea;

namespace ILSight;

/// <summary>What a host other than the app's window, a test, makes of adapters the root builds: each a function handed
/// the real adapter, which it may wrap (a decorator) or ignore (a fake); null keeps the real adapter.</summary>
public sealed record AdapterHooks
{
    /// <summary>The port the reopening of a list lists its files through.</summary>
    public Func<OpenAssembliesAsReloadHandler.IPorts, OpenAssembliesAsReloadHandler.IPorts>? Reopening { get; init; }

    /// <summary>The port a reload and a reopen of one assembly read the files again through.</summary>
    public Func<AssemblyReloading.IPorts, AssemblyReloading.IPorts>? Reloading { get; init; }

    /// <summary>The port a search's runs search the open assemblies through.</summary>
    public Func<RunSearchHandler.IPorts, RunSearchHandler.IPorts>? Searching { get; init; }

    /// <summary>The port a comparison's runs compare two assemblies through.</summary>
    public Func<CompareAssembliesHandler.IPorts, CompareAssembliesHandler.IPorts>? Comparing { get; init; }

    /// <summary>The port going to a bookmark looks its place up through.</summary>
    public Func<GoToBookmarkHandler.IPorts, GoToBookmarkHandler.IPorts>? GoingToBookmarks { get; init; }

    /// <summary>The port showing a type on a diagram reads its card and its neighbours' through.</summary>
    public Func<DiagramGrowing.IPorts, DiagramGrowing.IPorts>? Growing { get; init; }

    /// <summary>The port an export writes through.</summary>
    public Func<ExportWriting.IPorts, ExportWriting.IPorts>? Exporting { get; init; }

    /// <summary>The port the window is brought to the front through.</summary>
    public Func<IWindowFront, IWindowFront>? BringingToFront { get; init; }

    /// <summary>Handed the layout the root makes for <see cref="Panels"/>, which hands it to the framework's dock space:
    /// not an adapter, but what the dock widget writes itself (a drag docking or floating a panel, a close button), so
    /// a test host keeps it to stand in for the widget's own writes, as the dock space is handed it.</summary>
    public Action<MainLayout>? Layout { get; init; }
}
