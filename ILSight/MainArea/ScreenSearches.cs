using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.OmnibarArea;
using ILSight.SearchArea;

namespace ILSight.MainArea;

/// <summary>The screen's two searches, the search panel's and the omnibar's, run each frame.</summary>
internal static class ScreenSearches
{
    /// <summary>Has the search panel's search and the omnibar's follow their queries, each searching only while it is
    /// shown (the panel while it is in the layout, the omnibar while it searches), and asks for a frame when one of them
    /// waits for the user to stop typing.</summary>
    public static void Update(Ui ui, Panels panels, Search search, Omnibar omnibar, AssemblyList assemblies)
    {
        var shown = panels.IsOpen(DockPanels.Search);
        UpdateSearch(ui, search, assemblies, shown);
        UpdateSearch(ui, omnibar.Search, assemblies, omnibar.IsSearching);
    }

    private static void UpdateSearch(Ui ui, Search search, AssemblyList assemblies, bool shown)
    {
        var wait = search.Update(assemblies, TimeSpan.FromSeconds(ui.RealTime), shown);
        if (wait > TimeSpan.Zero)
        {
            ui.RequestUpdateIn(wait.TotalSeconds);
        }
    }
}
