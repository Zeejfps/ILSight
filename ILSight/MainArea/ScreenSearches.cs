using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.OmnibarArea;
using ILSight.SearchArea;

namespace ILSight.MainArea;

/// <summary>The screen's two searches, the search panel's and the omnibar's, run each frame.</summary>
internal static class ScreenSearches
{
    /// <summary>Runs the search panel's search and the omnibar's on their queries, each searching only while it is
    /// shown (the panel while it is in the layout, the omnibar while it searches), and asks for a frame when one of them
    /// waits for the user to stop typing.</summary>
    public static void RunShownSearches(Ui ui, Panels panels, Search search, Omnibar omnibar, AssemblyList assemblies)
    {
        var shown = panels.IsOpen(DockPanels.Search);
        RunSearch(ui, search, assemblies, shown);
        RunSearch(ui, omnibar.Search, assemblies, omnibar.IsSearching);
    }

    private static void RunSearch(Ui ui, Search search, AssemblyList assemblies, bool shown)
    {
        var wait = search.RunSearch(assemblies, TimeSpan.FromSeconds(ui.RealTime), shown);
        if (wait > TimeSpan.Zero)
        {
            ui.RequestUpdateIn(wait.TotalSeconds);
        }
    }
}
